namespace DMS_2025.REST.Messaging
{
    public sealed record OcrRequestMessage(
        Guid DocumentId,
        string Bucket,
        string ObjectName,
        string OriginalFileName
    );
}
