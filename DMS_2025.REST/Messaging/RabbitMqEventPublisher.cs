namespace DMS_2025.REST.Messaging;

using System.Text;
using System.Text.Json;
using DMS_2025.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;


public class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private readonly IConnectionFactory _factory;
    private readonly string _queue;
    private readonly ILogger<RabbitMqEventPublisher> _log;

    private IConnection? _conn;
    private IModel? _ch;
    private bool _queueDeclared;

    public RabbitMqEventPublisher(
        IConnectionFactory factory,
        IConfiguration cfg,
        ILogger<RabbitMqEventPublisher> log)
    {
        _factory = factory;
        _queue = cfg["RabbitMQ:Queue"]
                 ?? Environment.GetEnvironmentVariable("RABBITMQ__QUEUE")
                 ?? "documents";
        _log = log;
    }

    private void EnsureOpen()
    {
        if (_conn is { IsOpen: true } && _ch is { IsOpen: true } && _queueDeclared) return;

        // Alte, evtl. tote Verbindungen sauber schließen
        try { _ch?.Close(); } catch { /* ignore */ }
        try { _conn?.Close(); } catch { /* ignore */ }
        _ch?.Dispose();
        _conn?.Dispose();

        _conn = _factory.CreateConnection();     // <— erst jetzt verbinden
        _ch = _conn.CreateModel();
        _ch.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);
        _queueDeclared = true;
    }
    public Task PublishDocumentCreatedAsync(Document doc, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "DocumentCreated",
            id = doc.Id,
            title = doc.Title,
            createdUtc = DateTime.UtcNow
        });

        PublishCore(doc.Id.ToString(), payload);
        return Task.CompletedTask;
    }
    public Task PublishOcrRequestedAsync(OcrRequestMessage msg, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "OcrRequested",
            documentId = msg.DocumentId,
            bucket = msg.Bucket,
            objectName = msg.ObjectName,
            originalFileName = msg.OriginalFileName,
            createdUtc = DateTime.UtcNow
        });

        PublishCore(msg.DocumentId.ToString(), payload);
        return Task.CompletedTask;
    }
    private void PublishCore(string messageId, string json)
    {
        try
        {
            EnsureOpen();

            var body = Encoding.UTF8.GetBytes(json);
            var props = _ch!.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";
            props.MessageId = messageId;

            _ch.BasicPublish(exchange: "", routingKey: _queue, basicProperties: props, body: body);
            _log.LogInformation("Published message {MessageId} to queue {Queue}", messageId, _queue);
        }
        catch (Exception ex)
        {
            // Einmaliger Reconnect-Versuch (z.B. nach Broker-Restart)
            _log.LogWarning(ex, "Publish failed (first attempt). Reconnecting …");
            try
            {
                _queueDeclared = false;
                EnsureOpen();

                var body = Encoding.UTF8.GetBytes(json);
                var props = _ch!.CreateBasicProperties();
                props.Persistent = true;
                props.ContentType = "application/json";
                props.MessageId = messageId;

                _ch.BasicPublish(exchange: "", routingKey: _queue, basicProperties: props, body: body);
                _log.LogInformation("Published message {MessageId} to queue {Queue} after reconnect", messageId, _queue);
            }
            catch (Exception ex2)
            {
                // Nicht weiterwerfen – API soll nicht mit 500 sterben, nur loggen
                _log.LogError(ex2, "Failed to publish message {MessageId} to queue {Queue}", messageId, _queue);
            }
        }
    }
    public void Dispose()
    {
        try { _ch?.Close(); } catch { /* ignore */ }
        try { _conn?.Close(); } catch { /* ignore */ }
        _ch?.Dispose();
        _conn?.Dispose();
    }
}
