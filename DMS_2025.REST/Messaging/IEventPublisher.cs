namespace DMS_2025.REST.Messaging;

using DMS_2025.Models;
public interface IEventPublisher
{
    Task PublishDocumentCreatedAsync(Document doc, CancellationToken ct = default);
    Task PublishOcrRequestedAsync(OcrRequestMessage msg, CancellationToken ct = default);
}

