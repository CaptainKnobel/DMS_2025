using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DMS_2025.Models;

namespace DMS_2025.DAL.Repositories.Interfaces
{
    public interface IDocumentRepository
    {
        Task<Document?> GetAsync(Guid id, CancellationToken ct = default);
        Task AddAsync(Document doc, CancellationToken ct = default);
        Task UpdateAsync(Document doc, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
        IQueryable<Document> Query(); // for search/filter in service layer
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
