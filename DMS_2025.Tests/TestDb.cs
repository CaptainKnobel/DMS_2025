using System;
using DMS_2025.DAL.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DMS_2025.Tests;

public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn;
    public DmsDbContext Context { get; }

    public TestDb()
    {
        _conn = new SqliteConnection("Filename=:memory:");
        _conn.Open(); // keep open for the lifetime of the context

        var options = new DbContextOptionsBuilder<DmsDbContext>()
            .UseSqlite(_conn)
            .EnableSensitiveDataLogging()
            .Options;

        Context = new DmsDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _conn.Dispose();
    }
}
