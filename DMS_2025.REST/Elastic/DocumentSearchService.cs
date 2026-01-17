using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DMS_2025.REST.Elastic;

public sealed class DocumentSearchService : IDocumentSearchService
{
    private readonly ElasticsearchClient _es;
    private readonly string _index;

    public DocumentSearchService(ElasticsearchClient es, IOptions<ElasticSettings> settings)
    {
        _es = es;
        _index = settings.Value.IndexDocuments ?? "documents";
    }

    public async Task<IReadOnlyList<Guid>> SearchDocumentIdsAsync(
        string query, int page, int pageSize, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<Guid>();

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;

        var from = (page - 1) * pageSize;

        var res = await _es.SearchAsync<JsonElement>(s => s
            .Index(_index)
            .From(from)
            .Size(pageSize)
            .Query(q => q.Match(m => m
                .Field("textContent")
                .Query(query)
            )),ct);

        if (!res.IsValidResponse)
            return Array.Empty<Guid>();

        var ids = new List<Guid>();

        foreach (var hit in res.Hits)
        {
            if (hit.Source.ValueKind != JsonValueKind.Object) continue;

            if (hit.Source.TryGetProperty("id", out var idProp))
            {
                var idStr = idProp.GetString();
                if (Guid.TryParse(idStr, out var id))
                    ids.Add(id);
            }
        }

        return ids;
    }
}
