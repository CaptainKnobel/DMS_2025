using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DMS_2025.DAL.Context;
using DMS_2025.DAL.Repositories.EfCore;
using DMS_2025.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Moq;
using Moq.EntityFrameworkCore;
using NUnit.Framework;

namespace DMS_2025.Tests.DAL
{
    public class DocumentRepositoryTests
    {
        private Mock<DmsDbContext> _ctx = null!;
        private List<Document> _store = null!;
        private DocumentRepository _repo = null!;

        [SetUp]
        public void SetUp()
        {
            _store = new List<Document>();
            _ctx = new Mock<DmsDbContext>(new DbContextOptions<DmsDbContext>()) { CallBase = true };

            // Repo uses Set<T>(), so mock that to return a DbSet backed by our list
            _ctx.Setup(c => c.Set<Document>()).ReturnsDbSet(_store);

            // --- IMPORTANT: wire the behaviors Moq.EntityFrameworkCore may not auto-hook ---
            var setMock = Mock.Get(_ctx.Object.Set<Document>());

            // AddAsync -> add to list
            setMock
                .Setup(s => s.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
                .Callback<Document, CancellationToken>((e, _) => _store.Add(e))
                .ReturnsAsync((Document e, CancellationToken _) => (EntityEntry<Document>)null!);

            // Add (sync) just in case repo or future code paths use it
            setMock
                .Setup(s => s.Add(It.IsAny<Document>()))
                .Callback<Document>(e => _store.Add(e))
                .Returns((Document e) => (EntityEntry<Document>)null!);

            // Update -> replace by Id if present
            setMock
                .Setup(s => s.Update(It.IsAny<Document>()))
                .Callback<Document>(e =>
                {
                    var idx = _store.FindIndex(x => x.Id == e.Id);
                    if (idx >= 0) _store[idx] = e;
                })
                .Returns((Document e) => (EntityEntry<Document>)null!);

            // Remove -> remove by reference or by Id
            setMock
                .Setup(s => s.Remove(It.IsAny<Document>()))
                .Callback<Document>(e =>
                {
                    var existing = _store.FirstOrDefault(x => ReferenceEquals(x, e) || x.Id == e.Id);
                    if (existing != null) _store.Remove(existing);
                })
                .Returns((Document e) => (EntityEntry<Document>)null!);

            // Attach -> no-op (keeps DeleteAsync happy when it attaches then removes)
            setMock
                .Setup(s => s.Attach(It.IsAny<Document>()))
                .Callback<Document>(_ => { })
                .Returns((Document e) => (EntityEntry<Document>)null!);

            // pretend persistence succeeded
            _ctx.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            _repo = new DocumentRepository(_ctx.Object);
        }

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
            // seed directly into the backing store
            _store.Add(doc);

            doc.Title = "v2";
            await _repo.UpdateAsync(doc);
            await _repo.SaveChangesAsync();

            var loaded = await _repo.GetAsync(doc.Id);
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Title, Is.EqualTo("v2"));
        }

        [Test]
        public async Task Delete_Works_When_Entity_Exists()
        {
            var id = Guid.NewGuid();
            _store.Add(new Document { Id = id, Title = "to-delete" });

            await _repo.DeleteAsync(id);
            await _repo.SaveChangesAsync();

            var loaded = await _repo.GetAsync(id);
            Assert.That(loaded, Is.Null);
        }

        [Test]
        public async Task Delete_NoThrow_When_Entity_Missing()
        {
            await _repo.DeleteAsync(Guid.NewGuid());
            await _repo.SaveChangesAsync();
            Assert.Pass();
        }

        [Test]
        public void Query_Can_Filter()
        {
            _store.AddRange(new[]
            {
                new Document { Id = Guid.NewGuid(), Title = "Alpha" },
                new Document { Id = Guid.NewGuid(), Title = "Beta" }
            });

            var betas = _repo.Query().Where(d => d.Title!.Contains("Beta")).ToList();

            Assert.That(betas, Has.Count.EqualTo(1));
            Assert.That(betas[0].Title, Is.EqualTo("Beta"));
        }

        [Test]
        public async Task CancellationToken_Overloads_Work()
        {
            using var cts = new CancellationTokenSource();

            await _repo.AddAsync(new Document { Id = Guid.NewGuid(), Title = "CT" }, cts.Token);
            await _repo.SaveChangesAsync(cts.Token);
            _ = await _repo.GetAsync(Guid.NewGuid(), cts.Token);

            Assert.Pass();
        }

        [Test]
        public void SaveChanges_Concurrency_Throws()
        {
            var ctx = new Mock<DmsDbContext>(new DbContextOptions<DmsDbContext>()) { CallBase = true };
            var store = new System.Collections.Generic.List<Document>();
            ctx.Setup(c => c.Set<Document>()).ReturnsDbSet(store);

            var repo = new DocumentRepository(ctx.Object);

            ctx.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
               .ThrowsAsync(new DbUpdateConcurrencyException());

            Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
            {
                await repo.UpdateAsync(new Document { Id = Guid.NewGuid(), Title = "x" });
                await repo.SaveChangesAsync();
            });
        }
    }
}
