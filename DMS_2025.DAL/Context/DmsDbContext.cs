using System.Collections.Generic;
using DMS_2025.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DMS_2025.DAL.Context
{
    public class DmsDbContext : DbContext
    {
        private readonly IConfiguration _configuration;

        public DbSet<Document> Documents { get; set; }

        public DmsDbContext(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(_configuration.GetConnectionString("DefaultConnection"));
        }
    }
}

