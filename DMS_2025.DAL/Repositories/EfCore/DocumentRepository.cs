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

        public IQueryable<Document> Query() =>
            _db.Set<Document>(); // DbSet<T> already implements IQueryable<T>

        public Task<Document?> GetAsync(Guid id, CancellationToken ct = default) =>
            _db.Set<Document>()
               .AsNoTracking()
               .FirstOrDefaultAsync(x => x.Id == id, ct);

        public Task AddAsync(Document doc, CancellationToken ct = default) =>
            _db.Set<Document>().AddAsync(doc, ct).AsTask();

        public Task UpdateAsync(Document doc, CancellationToken ct = default)
        {
            _db.Set<Document>().Update(doc);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var entity = await GetAsync(id, ct);
            if (entity != null)
            {
                // Reattach so Remove works when GetAsync used AsNoTracking
                _db.Set<Document>().Attach(entity);
                _db.Set<Document>().Remove(entity);
            }
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            _db.SaveChangesAsync(ct);
    }
}
