using System;
using DMS_2025.DAL.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DMS_2025.DAL.Context
{
    public class DmsDbContextFactory : IDesignTimeDbContextFactory<DmsDbContext>
    {
        public DmsDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DmsDbContext>();

            // same default as in Program.cs, env var wins if present
            var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                     ?? "Host=localhost;Port=5432;Database=dms_db;Username=postgres;Password=postgres";

            optionsBuilder.UseNpgsql(cs);
            return new DmsDbContext(optionsBuilder.Options);
        }
    }
}
