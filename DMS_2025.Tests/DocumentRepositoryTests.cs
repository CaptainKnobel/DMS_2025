using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DMS_2025.DAL.Repositories.EfCore;
using DMS_2025.Models;
using NUnit.Framework;

namespace DMS_2025.Tests;

public class DocumentRepositoryTests
{
    private TestDb _db = null!;
    private DocumentRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _db = new TestDb();
        _repo = new DocumentRepository(_db.Context);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task Add_Then_Get_Works()
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            Title = "Hello",
            Location = "/files/hello.pdf",
            CreationDate = DateTime.UtcNow,
            Author = "tester"
        };

        await _repo.AddAsync(doc);
        await _repo.SaveChangesAsync();

        var loaded = await _repo.GetAsync(doc.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.Title, Is.EqualTo("Hello"));
    }

    [Test]
    public async Task Update_Works()
    {
        var doc = new Document { Id = Guid.NewGuid(), Title = "v1" };
        await _repo.AddAsync(doc);
        await _repo.SaveChangesAsync();

        doc.Title = "v2";
        await _repo.UpdateAsync(doc);
        await _repo.SaveChangesAsync();

        var loaded = await _repo.GetAsync(doc.Id);
        Assert.That(loaded!.Title, Is.EqualTo("v2"));
    }

    [Test]
    public async Task Delete_Works_When_Entity_Exists()
    {
        var id = Guid.NewGuid();
        await _repo.AddAsync(new Document { Id = id, Title = "to-delete" });
        await _repo.SaveChangesAsync();

        await _repo.DeleteAsync(id);
        await _repo.SaveChangesAsync();

        var loaded = await _repo.GetAsync(id);
        Assert.That(loaded, Is.Null);
    }

    [Test]
    public async Task Delete_NoThrow_When_Entity_Missing()
    {
        // Should be a no-op
        await _repo.DeleteAsync(Guid.NewGuid());
        Assert.Pass(); // if we got here, no exception = OK
    }

    [Test]
    public async Task Query_Can_Filter()
    {
        await _repo.AddAsync(new Document { Id = Guid.NewGuid(), Title = "Alpha" });
        await _repo.AddAsync(new Document { Id = Guid.NewGuid(), Title = "Beta" });
        await _repo.SaveChangesAsync();

        var betas = _repo.Query().Where(d => d.Title!.Contains("Beta")).ToList();
        Assert.That(betas, Has.Count.EqualTo(1));
        Assert.That(betas[0].Title, Is.EqualTo("Beta"));
    }

    [Test]
    public async Task Methods_Honor_CancellationToken_Overloads()
    {
        using var cts = new CancellationTokenSource();
        await _repo.AddAsync(new Document { Id = Guid.NewGuid(), Title = "CT" }, cts.Token);
        _ = await _repo.GetAsync(Guid.NewGuid(), cts.Token);
        Assert.Pass();
    }
}
