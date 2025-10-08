// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System.Text;

await Host.CreateDefaultBuilder(args)
    .UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console())
    .ConfigureServices((ctx, services) =>
    {
    var uri = ctx.Configuration["RabbitMQ:Uri"]
        ?? Environment.GetEnvironmentVariable("RABBITMQ__URI")
        ?? "amqp://guest:guest@rabbitmq:5672";
    var factory = new ConnectionFactory {
        Uri = new Uri(uri),
        DispatchConsumersAsync = true,
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled = true,                     // (re-declare queues, QoS, consumer)
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        RequestedHeartbeat = TimeSpan.FromSeconds(30),      // hilft Timeouts
        ClientProvidedName = "dms_2025-worker"              // schöner im RMQ UI
    };
    services.AddSingleton(factory.CreateConnection());
    services.AddHostedService<QueueConsumer>();
    })
    .RunConsoleAsync();

public class QueueConsumer : BackgroundService
{
    private readonly IConnection _conn;
    private readonly string _queue;
    private readonly ILogger<QueueConsumer> _log;

    public QueueConsumer(IConnection conn, IConfiguration cfg, ILogger<QueueConsumer> log)
    {
        _conn = conn;
        _queue = cfg["RabbitMQ:Queue"]
            ?? Environment.GetEnvironmentVariable("RABBITMQ__QUEUE")
            ?? "documents";
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ch = _conn.CreateModel();
        _conn.ConnectionShutdown += (_, ea) =>
            _log.LogWarning("RMQ connection shutdown: {ReplyText} ({Code})", ea.ReplyText, ea.ReplyCode);

        ch.ModelShutdown += (_, ea) =>
            _log.LogWarning("RMQ channel shutdown: {ReplyText} ({Code})", ea.ReplyText, ea.ReplyCode);

        ch.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);
        ch.BasicQos(0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.Shutdown += async (_, ea) =>
        {
            _log.LogWarning("RMQ consumer shutdown: {ReplyText} ({Code})", ea.ReplyText, ea.ReplyCode);
            await Task.CompletedTask;
        };
        consumer.Registered += async (_, ea) =>
        {
            _log.LogInformation("RMQ consumer registered");
            await Task.CompletedTask;
        };
        consumer.Unregistered += async (_, ea) =>
        {
            _log.LogInformation("RMQ consumer unregistered");
            await Task.CompletedTask;
        };
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var msg = Encoding.UTF8.GetString(ea.Body.ToArray());
                _log.LogInformation("Worker received: {Message}", msg);
                // TODO: OCR
                ch.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error while processing message");
                ch.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
            await Task.CompletedTask;
        };
        var consumerTag = ch.BasicConsume(_queue, autoAck: false, consumer);
        _log.LogInformation("BasicConsume started. ConsumerTag: {Tag}", consumerTag);

        // Blockieren bis zum Shutdown:
        var tcs = new TaskCompletionSource<object?>();
        stoppingToken.Register(() =>
        {
            try { ch.Close(); ch.Dispose(); } catch { }
            tcs.TrySetResult(null);
        });
        return tcs.Task;
    }
}