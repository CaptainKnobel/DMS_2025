using DMS_2025.Services.Worker.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DMS_2025.Services.Worker.Elastic;

public sealed class DocumentIndexService : IDocumentIndexService
{
    private readonly global::Elastic.Clients.Elasticsearch.ElasticsearchClient _es;
    private readonly ElasticSettings _cfg;
    private readonly ILogger<DocumentIndexService> _log;

    public DocumentIndexService(
        global::Elastic.Clients.Elasticsearch.ElasticsearchClient es,
        IOptions<ElasticSettings> cfg,
        ILogger<DocumentIndexService> log)
    {
        _es = es;
        _cfg = cfg.Value;
        _log = log;
    }

    public async Task EnsureIndexAsync(CancellationToken ct)
    {
        var index = _cfg.DocumentsIndex;

        for (var attempt = 1; attempt <= 15; attempt++)
        {
            try
            {
                var exists = await _es.Indices.ExistsAsync(index, ct);
                if (exists.Exists) return;

                _log.LogInformation("Creating Elasticsearch index {Index}", index);

                var create = await _es.Indices.CreateAsync(index, ct);
                if (!create.IsValidResponse)
                    throw new InvalidOperationException($"Failed to create index {index}: {create.DebugInformation}");

                return;
            }
            catch (Exception ex) when (attempt < 15 && !ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Elasticsearch not ready (attempt {Attempt}). Retry in 2s …", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }


    public async Task IndexAsync(DocumentIndexItem item, CancellationToken ct)
    {
        if (item.Id == Guid.Empty)
            throw new ArgumentException("Id required", nameof(item));

        item.TextContent ??= string.Empty;

        var index = _cfg.DocumentsIndex;

        var resp = await _es.IndexAsync(item, i => i
            .Index(index)
            .Id(item.Id), ct);

        if (!resp.IsValidResponse)
            throw new InvalidOperationException($"Failed to index doc {item.Id}: {resp.DebugInformation}");
    }
}
