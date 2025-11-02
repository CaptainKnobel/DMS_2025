// See https://aka.ms/new-console-template for more information
using DMS_2025.Services.Worker.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using System;
using System.Text;
using System.Text.Json;

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
        services.AddSingleton(factory);                 // nur Factory registrieren

        // MinIO Settings from ENV / Configuration binding (MINIO__* mapped on "Minio:*")
        services.Configure<MinioSettings>(ctx.Configuration.GetSection("Minio"));

        // register MinIO Client
        services.AddSingleton<IMinioClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<MinioSettings>>().Value;
            return new MinioClient()
                .WithEndpoint(cfg.Endpoint)
                .WithCredentials(cfg.AccessKey, cfg.SecretKey)
                .WithSSL(cfg.UseSSL)
                .Build();
        });

        // register QueueConsumer
        services.AddHostedService<QueueConsumer>();
    })
    .RunConsoleAsync();

public class QueueConsumer : BackgroundService
{
    private readonly ConnectionFactory _factory;
    private IConnection? _conn;
    private IModel? _ch;
    private readonly string _queue;
    private readonly ILogger<QueueConsumer> _log;
    private readonly int _demoDelayMs;
    private readonly IMinioClient _minio;

    public QueueConsumer(ConnectionFactory factory, IConfiguration cfg, ILogger<QueueConsumer> log, IMinioClient minio)
    {
        _factory = factory;
        _queue = cfg["RabbitMQ:Queue"]
            ?? Environment.GetEnvironmentVariable("RABBITMQ__QUEUE")
            ?? "documents";
        _demoDelayMs = int.TryParse(cfg["WORKER_DEMO_DELAY_MS"], out var ms) ? ms : 0;
        _log = log;
        _minio = minio;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Lazy connect + kleiner Retry
        for (var attempt = 1; _conn is null || !_conn.IsOpen; attempt++)
        {
            try
            {
                _conn = _factory.CreateConnection();
                _ch = _conn.CreateModel();
            }
            catch (Exception ex) when (attempt < 15 && !stoppingToken.IsCancellationRequested)
            {
                _log.LogWarning(ex, "RabbitMQ noch nicht bereit (Versuch {Attempt}). Retry in 2s …", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        var ch = _ch!;

        _conn!.ConnectionShutdown += (_, ea) =>
            _log.LogWarning("RMQ connection shutdown: {ReplyText} ({Code})", ea.ReplyText, ea.ReplyCode);
        ch.ModelShutdown += (_, ea) =>
            _log.LogWarning("RMQ channel shutdown: {ReplyText} ({Code})", ea.ReplyText, ea.ReplyCode);

        ch.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);
        ch.BasicQos(0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.Registered += (_, __) => { _log.LogInformation("RMQ consumer registered"); return Task.CompletedTask; };
        consumer.Unregistered += (_, __) => { _log.LogInformation("RMQ consumer unregistered"); return Task.CompletedTask; };
        consumer.Shutdown += (_, ea) => { _log.LogWarning("RMQ consumer shutdown: {ReplyText} ({Code})", ea.ReplyText, ea.ReplyCode); return Task.CompletedTask; };

        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

                if (string.Equals(type, "OcrRequested", StringComparison.OrdinalIgnoreCase))
                {
                    var bucket = root.GetProperty("bucket").GetString()!;
                    var objectName = root.GetProperty("objectName").GetString()!;
                    var original = root.GetProperty("originalFileName").GetString();

                    var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
                    await using (var fs = File.Create(tmp))
                    {
                        await _minio.GetObjectAsync(new GetObjectArgs()
                            .WithBucket(bucket)
                            .WithObject(objectName)
                            .WithCallbackStream(s => s.CopyTo(fs)), cancellationToken: stoppingToken);
                    }

                    var text = await RunTesseractAsync(tmp, stoppingToken);
                    _log.LogInformation("OCR for {Original}: {Preview}...", original, text.Length > 160 ? text[..160] : text);
                }
                else
                {
                    _log.LogInformation("Ignoring message type {Type}", type ?? "<unknown>");
                }

                ch.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error while processing message");
                ch.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        var tag = ch.BasicConsume(_queue, autoAck: false, consumer);
        _log.LogInformation("BasicConsume started. ConsumerTag: {Tag}", tag);

        // Auf Shutdown warten
        var tcs = new TaskCompletionSource<object?>();
        stoppingToken.Register(() =>
        {
            try { ch.Close(); } catch { }
            try { ch.Dispose(); } catch { }
            try { _conn?.Close(); } catch { }
            try { _conn?.Dispose(); } catch { }
            tcs.TrySetResult(null);
        });
        await tcs.Task;
    }
    private static async Task<string> RunTesseractAsync(string pdfPath, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "tesseract",
            Arguments = $"\"{pdfPath}\" stdout -l deu+eng",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = System.Diagnostics.Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync(ct);
        var err = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0) throw new Exception("tesseract failed: " + err);
        return output;
    }
}