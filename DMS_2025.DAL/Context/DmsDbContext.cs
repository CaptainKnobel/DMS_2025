using DMS_2025.Models;
using Microsoft.EntityFrameworkCore;

namespace DMS_2025.DAL.Context
{
    public class DmsDbContext : DbContext
    {
        public DmsDbContext(DbContextOptions<DmsDbContext> options) : base(options) { }

        public DbSet<Document> Documents => Set<Document>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // add entity configurations here later if needed
            base.OnModelCreating(modelBuilder);
        }
    }
}
