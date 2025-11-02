namespace DMS_2025.REST.Messaging;

using System.Text;
using System.Text.Json;
using DMS_2025.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;


public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IConnection _conn;
    private readonly string _queue;
    private readonly ILogger<RabbitMqEventPublisher> _log;

    public RabbitMqEventPublisher(IConnection conn, IConfiguration cfg, ILogger<RabbitMqEventPublisher> log)
    {
        _conn = conn;
        _queue = cfg["RabbitMQ:Queue"]
            ?? Environment.GetEnvironmentVariable("RABBITMQ__QUEUE")
            ?? "documents";
        _log = log;
    }

    public Task PublishDocumentCreatedAsync(Document doc, CancellationToken ct = default)
    {
        try
        {
            using var ch = _conn.CreateModel();
            ch.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);
            var payload = JsonSerializer.Serialize(new
            {
                type = "DocumentCreated",
                id = doc.Id,
                title = doc.Title,
                createdUtc = DateTime.UtcNow
            });
            var body = Encoding.UTF8.GetBytes(payload);
            var props = ch.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";
            props.MessageId = doc.Id.ToString();
            ch.BasicPublish(exchange: "", routingKey: _queue, basicProperties: props, body: body);
            _log.LogInformation("Published DocumentCreated for {DocId}", doc.Id);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to publish DocumentCreated for {DocId}", doc.Id);
            // nicht werfen -> API bleibt erfolgreich
        }
        return Task.CompletedTask;
    }
    public Task PublishOcrRequestedAsync(OcrRequestMessage msg, CancellationToken ct = default)
    {
        try
        {
            using var ch = _conn.CreateModel();
            ch.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);

            var payload = JsonSerializer.Serialize(new
            {
                type = "OcrRequested",
                documentId = msg.DocumentId,
                bucket = msg.Bucket,
                objectName = msg.ObjectName,
                originalFileName = msg.OriginalFileName,
                createdUtc = DateTime.UtcNow
            });

            var body = Encoding.UTF8.GetBytes(payload);
            var props = ch.CreateBasicProperties();
            props.Persistent = true;
            props.ContentType = "application/json";
            props.MessageId = msg.DocumentId.ToString();

            ch.BasicPublish(exchange: "", routingKey: _queue, basicProperties: props, body: body);
            _log.LogInformation("Published OcrRequested for {DocId}", msg.DocumentId);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to publish OcrRequested for {DocId}", msg.DocumentId);
        }
        return Task.CompletedTask;
    }
}
