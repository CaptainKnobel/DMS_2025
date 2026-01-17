using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMS_2025.Services.Worker.Elastic
{
    public interface IDocumentIndexService
    {
        Task EnsureIndexAsync(CancellationToken ct);
        Task IndexAsync(DocumentIndexItem item, CancellationToken ct);
    }
}
