using DMS_2025.DAL.Repositories.Interfaces;
using DMS_2025.Models;
using DMS_2025.REST;
using DMS_2025.REST.Controllers.V1;
using DMS_2025.REST.DTOs;
using DMS_2025.REST.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DMS_2025.Tests.REST
{
    public class DocumentsControllerTests
    {
        private Mock<IDocumentRepository> _repo = null!;
        private Mock<IEventPublisher> _pub = null!;
        private DocumentsController _ctrl = null!;
        private string _tmpDir = null!;

        [SetUp]
        public void SetUp()
        {
            _repo = new Mock<IDocumentRepository>(MockBehavior.Strict);
            _pub = new Mock<IEventPublisher>(MockBehavior.Strict);

            _tmpDir = Path.Combine(Path.GetTempPath(), "dms_tests_" + Guid.NewGuid());
            Directory.CreateDirectory(_tmpDir);

            var root = new UploadRoot(_tmpDir);
            _ctrl = new DocumentsController(_repo.Object, _pub.Object, root);
        }

        [TearDown]
        public void TearDown()
        {
            _repo.VerifyNoOtherCalls();
            _pub.VerifyNoOtherCalls();
            try { if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true); }
            catch { /* egal im Test :) */ }
        }

        [Test]
        public async Task Get_Returns_404_When_Missing()
        {
            var id = Guid.NewGuid();
            _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

            var res = await _ctrl.Get(id, CancellationToken.None);

            Assert.That(res.Result, Is.InstanceOf<NotFoundResult>());
            _repo.Verify(r => r.GetAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Get_Returns_200_With_Document()
        {
            var d = new Document { Id = Guid.NewGuid(), Title = "Hello", Author = "tester", CreationDate = DateTime.UtcNow, Location = "/x" };
            _repo.Setup(r => r.GetAsync(d.Id, It.IsAny<CancellationToken>())).ReturnsAsync(d);

            var res = await _ctrl.Get(d.Id, CancellationToken.None);
            var ok = res.Result as OkObjectResult;

            Assert.That(ok, Is.Not.Null);
            var dto = ok!.Value as DocumentResponse;
            Assert.That(dto, Is.Not.Null);
            Assert.That(dto!.Title, Is.EqualTo("Hello"));

            _repo.Verify(r => r.GetAsync(d.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Create_Persists_Publishes_And_Returns_201()
        {
            var req = new DocumentCreateRequest
            {
                Title = "New Doc",
                Location = "/docs/new.pdf",
                CreationDate = null,
                Author = "alice"
            };

            _repo.Setup(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _pub.Setup(p => p.PublishDocumentCreatedAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var res = await _ctrl.Create(req, CancellationToken.None);
            var created = res.Result as CreatedAtActionResult;

            Assert.That(created, Is.Not.Null);
            Assert.That(created!.ActionName, Is.EqualTo(nameof(DocumentsController.Get)));
            Assert.That(created.Value, Is.InstanceOf<DocumentResponse>());

            _repo.Verify(r => r.AddAsync(It.Is<Document>(d => d.Title == "New Doc" && d.Author == "alice"), It.IsAny<CancellationToken>()), Times.Once);
            _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _pub.Verify(p => p.PublishDocumentCreatedAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Update_Returns_404_When_Missing()
        {
            var id = Guid.NewGuid();
            _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Document?)null);

            var res = await _ctrl.Update(id, new DocumentUpdateRequest { Title = "x" }, CancellationToken.None);

            Assert.That(res, Is.InstanceOf<NotFoundResult>());
            _repo.Verify(r => r.GetAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Update_Updates_And_Returns_204()
        {
            var id = Guid.NewGuid();
            var entity = new Document { Id = id, Title = "old", Author = "a", Location = "/old", CreationDate = DateTime.UtcNow.AddDays(-1) };
            _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
            _repo.Setup(r => r.UpdateAsync(entity, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            var req = new DocumentUpdateRequest { Title = "new", Author = "b" };
            var res = await _ctrl.Update(id, req, CancellationToken.None);

            Assert.That(res, Is.InstanceOf<NoContentResult>());
            Assert.That(entity.Title, Is.EqualTo("new"));
            Assert.That(entity.Author, Is.EqualTo("b"));

            _repo.Verify(r => r.GetAsync(id, It.IsAny<CancellationToken>()), Times.Once);
            _repo.Verify(r => r.UpdateAsync(It.Is<Document>(d => d.Id == id && d.Title == "new" && d.Author == "b"), It.IsAny<CancellationToken>()), Times.Once);
            _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Delete_Deletes_And_Returns_204()
        {
            var id = Guid.NewGuid();
            _repo.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Document { Id = id, FilePath = null });

            _repo.Setup(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            var res = await _ctrl.Delete(id, CancellationToken.None);

            Assert.That(res, Is.InstanceOf<NoContentResult>());
            _repo.Verify(r => r.GetAsync(id, It.IsAny<CancellationToken>()), Times.Once);
            _repo.Verify(r => r.DeleteAsync(id, It.IsAny<CancellationToken>()), Times.Once);
            _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Upload_Saves_File_Persists_And_Publishes()
        {
            // Arrange a fake IFormFile
            var content = new MemoryStream();
            using (var writer = new StreamWriter(content, leaveOpen: true))
            {
                writer.Write("hello world");
            }
            content.Position = 0;

            var formFile = new Mock<IFormFile>();
            formFile.SetupGet(f => f.FileName).Returns("doc.txt");
            formFile.SetupGet(f => f.Length).Returns(content.Length);
            formFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .Returns<Stream, CancellationToken>((s, ct) => content.CopyToAsync(s, ct));

            var req = new DocumentUploadRequest
            {
                Title = "Upload",
                Location = "/uploaded",
                CreationDate = null,
                Author = "uploader",
                File = formFile.Object
            };

            _repo.Setup(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            _pub.Setup(p => p.PublishDocumentCreatedAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            var res = await _ctrl.Upload(req, CancellationToken.None);
            var created = res as CreatedAtActionResult;

            // Assert
            Assert.That(created, Is.Not.Null);
            Assert.That(created!.ActionName, Is.EqualTo(nameof(DocumentsController.Get)));
            Assert.That(created.Value, Is.InstanceOf<DocumentResponse>());

            _repo.Verify(r => r.AddAsync(It.Is<Document>(d => d.Title == "Upload" && d.Author == "uploader"), It.IsAny<CancellationToken>()), Times.Once);
            _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
            _pub.Verify(p => p.PublishDocumentCreatedAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
