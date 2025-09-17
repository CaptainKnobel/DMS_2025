using DMS_2025.DAL.Context;
using DMS_2025.DAL.Repositories.Interfaces;
using DMS_2025.Models;
using Microsoft.EntityFrameworkCore;

namespace DMS_2025.DAL.Repositories.EfCore
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly DmsDbContext _db;
        public DocumentRepository(DmsDbContext db) => _db = db;

        public IQueryable<Document> Query() => _db.Documents.AsQueryable();

        public Task<Document?> GetAsync(Guid id, CancellationToken ct = default) =>
            _db.Documents.FirstOrDefaultAsync(x => x.Id == id, ct);

        public Task AddAsync(Document doc, CancellationToken ct = default) =>
            _db.Documents.AddAsync(doc, ct).AsTask();

        public Task UpdateAsync(Document doc, CancellationToken ct = default)
        {
            _db.Documents.Update(doc);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await GetAsync(id, ct);
            if (entity != null) _db.Documents.Remove(entity);
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            _db.SaveChangesAsync(ct);
    }
}
