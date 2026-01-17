namespace DMS_2025.REST.Elastic
{
    public interface IDocumentSearchService
    {
        Task<IReadOnlyList<Guid>> SearchDocumentIdsAsync(
            string query,
            int page,
            int pageSize,
            CancellationToken ct);
    }
}
