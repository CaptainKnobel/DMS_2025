using DMS_2025.DAL.Repositories.Interfaces; // IDocumentRepository
using DMS_2025.Models;                       // Document (Entity)
using DMS_2025.REST.DTOs;
using DMS_2025.REST.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;
using DMS_2025.REST.Config;

namespace DMS_2025.REST.Controllers.V1
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repo;
        private readonly IEventPublisher _pub;
        private readonly string _root;
        private readonly IMinioClient _minio;
        private readonly MinioSettings _minioCfg;
        public DocumentsController(
            IDocumentRepository repo,
            IEventPublisher pub,
            UploadRoot root,
            IMinioClient minio,
            IOptions<MinioSettings> minioCfg)
        {
            _repo = repo;
            _pub = pub;
            _root = Path.GetFullPath(root.Path);
            Directory.CreateDirectory(_root);
            _minio = minio;
            _minioCfg = minioCfg.Value;
        }

        // ----- Helper
        private string SaveFile(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName);
            var stored = $"{Guid.NewGuid()}{ext}".ToLowerInvariant();
            var full = Path.GetFullPath(Path.Combine(_root, stored));

            if (!full.StartsWith(_root, StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe path.");

            using var fs = System.IO.File.Create(full);
            file.CopyTo(fs);
            return full;
        }

        private void SafeDelete(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var full = Path.GetFullPath(path);
            if (full.StartsWith(_root, StringComparison.Ordinal) && System.IO.File.Exists(full))
                System.IO.File.Delete(full);
        }

        private static DocumentResponse ToResponse(Document d)
        {
            return new DocumentResponse
            {
                Id = d.Id,
                Title = d.Title,
                Location = d.Location,
                CreationDate = d.CreationDate,
                Author = d.Author,
                HasFile = !string.IsNullOrWhiteSpace(d.FilePath),
                FileSize = d.FileSize,
                OriginalFileName = d.OriginalFileName,
                Summary = d.Summary
            };
        }


        /// GET /api/v1/documents?page=&pageSize=&q=&sort=
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentResponse>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? q = null,
            [FromQuery] string? sort = "created_desc",  // simple default
            CancellationToken ct = default)
        {
            if (page <= 0) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 20;

            var query = _repo.Query(); // IQueryable<Document>

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(d =>
                    (d.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (d.Location ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (d.Author ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            query = sort?.ToLowerInvariant() switch
            {
                "title_asc" => query.OrderBy(d => d.Title),
                "title_desc" => query.OrderByDescending(d => d.Title),
                "created_asc" => query.OrderBy(d => d.CreationDate),
                _ => query.OrderByDescending(d => d.CreationDate) // created_desc
            };

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            Response.Headers["X-Total-Count"] = total.ToString();

            //var result = items.Select(d => new DocumentResponse
            //{
            //    Id = d.Id,
            //    Title = d.Title,
            //    Location = d.Location,
            //    CreationDate = d.CreationDate,
            //    Author = d.Author,
            //    HasFile = !string.IsNullOrWhiteSpace(d.FilePath),
            //    FileSize = d.FileSize,
            //    OriginalFileName = d.OriginalFileName
            //});

            var result = items.Select(ToResponse);

            return Ok(result);
        }

        /// GET /api/v1/documents/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DocumentResponse>> Get(Guid id, CancellationToken ct)
        {
            var d = await _repo.GetAsync(id, ct);
            return d is null
                ? NotFound()
                : Ok(ToResponse(d));
                //: Ok(new DocumentResponse
                //{
                //    Id = d.Id,
                //    Title = d.Title,
                //    Location = d.Location,
                //    CreationDate = d.CreationDate,
                //    Author = d.Author,
                //    HasFile = !string.IsNullOrWhiteSpace(d.FilePath),
                //    FileSize = d.FileSize,
                //    OriginalFileName = d.OriginalFileName
                //});
        }

        // Download
        [HttpGet("{id:guid}/file")]
        public async Task<IActionResult> Download(Guid id, CancellationToken ct)
        {
            var d = await _repo.GetAsync(id, ct);
            if (d is null || string.IsNullOrWhiteSpace(d.FilePath) || !System.IO.File.Exists(d.FilePath))
            {
                return NotFound();
            }

            var stream = System.IO.File.OpenRead(d.FilePath); // TODO: maybe remove?
            var contentType = string.IsNullOrWhiteSpace(d.ContentType) ? "application/octet-stream" : d.ContentType;
            var downloadName = string.IsNullOrWhiteSpace(d.OriginalFileName) ? Path.GetFileName(d.FilePath) : d.OriginalFileName;

            return File(stream, contentType, downloadName, enableRangeProcessing: true); // TODO: maybe: return PhysicalFile(d.FilePath, contentType, downloadName, enableRangeProcessing: true);
        }

        /// POST /api/v1/documents
        [HttpPost]
        public async Task<ActionResult<DocumentResponse>> Create([FromBody] DocumentCreateRequest req, CancellationToken ct)
        {
            var entity = new Document
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                Location = req.Location,
                CreationDate = req.CreationDate ?? DateTime.UtcNow, // default
                Author = req.Author
            };

            await _repo.AddAsync(entity, ct);
            await _repo.SaveChangesAsync(ct); // WICHTIG
            await _pub.PublishDocumentCreatedAsync(entity, ct);

            //var dto = new DocumentResponse
            //{
            //    Id = entity.Id,
            //    Title = entity.Title,
            //    Location = entity.Location,
            //    CreationDate = entity.CreationDate,
            //    Author = entity.Author,
            //    HasFile = !string.IsNullOrWhiteSpace(entity.FilePath),
            //    FileSize = entity.FileSize,
            //    OriginalFileName = entity.OriginalFileName
            //};

            // return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);

            var dto = ToResponse(entity);
            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }

        // upload
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20L * 1024 * 1024)]
        public async Task<IActionResult> Upload([FromForm] DocumentUploadRequest req, CancellationToken ct)
        {
            // Persist the file to disk (temp for Sprint 2)
            var fullPath = SaveFile(req.File);

            // Persist metadata (let's maybe add a file reference column in the entity later??)
            var entity = new Document
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                Location = req.Location,
                CreationDate = req.CreationDate ?? DateTime.UtcNow,
                Author = req.Author,
                FilePath = fullPath,
                OriginalFileName = req.File.FileName,
                ContentType = req.File.ContentType,
                FileSize = req.File.Length
            };
            await _repo.AddAsync(entity, ct);
            await _repo.SaveChangesAsync(ct);

            // upload in MinIO
            var exists = await _minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(_minioCfg.Bucket), ct);
            if (!exists)
                await _minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(_minioCfg.Bucket), ct);

            var safeName = Path.GetFileName(req.File.FileName);
            var objectName = $"{entity.Id}/{Guid.NewGuid()}_{safeName}";

            await using (var s = req.File.OpenReadStream())
            {
                await _minio.PutObjectAsync(new PutObjectArgs()
                    .WithBucket(_minioCfg.Bucket)
                    .WithObject(objectName)
                    .WithStreamData(s)
                    .WithObjectSize(req.File.Length)
                    .WithContentType(req.File.ContentType ?? "application/pdf"), ct);
            }

            // Events + OCR-Event
            await _pub.PublishDocumentCreatedAsync(entity, ct);
            await _pub.PublishOcrRequestedAsync(new OcrRequestMessage(
                entity.Id,
                _minioCfg.Bucket,
                objectName,
                safeName
            ), ct);

            // Response: Return Created with a DTO
            //return CreatedAtAction(nameof(Get), new { id = entity.Id }, new DocumentResponse
            //{
            //    Id = entity.Id,
            //    Title = entity.Title,
            //    Location = entity.Location,
            //    CreationDate = entity.CreationDate,
            //    Author = entity.Author,
            //    HasFile = true,
            //    FileSize = entity.FileSize,
            //    OriginalFileName = entity.OriginalFileName
            //});

            return CreatedAtAction(nameof(Get), new { id = entity.Id }, ToResponse(entity));
        }

        /// PUT /api/v1/documents/{id}
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] DocumentUpdateRequest req, CancellationToken ct)
        {
            var d = await _repo.GetAsync(id, ct);
            if (d is null) return NotFound();

            d.Title = req.Title ?? d.Title;
            d.Location = req.Location ?? d.Location;
            d.CreationDate = req.CreationDate ?? d.CreationDate;
            d.Author = req.Author ?? d.Author;

            await _repo.UpdateAsync(d, ct);
            await _repo.SaveChangesAsync(ct); // WICHTIG

            return NoContent();
        }

        [HttpPut("{id:guid}/file")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20L * 1024 * 1024)]
        public async Task<IActionResult> ReplaceFile(Guid id, IFormFile file, CancellationToken ct)
        {
            if (file is null) return BadRequest(new { error = "file is required" });

            var d = await _repo.GetAsync(id, ct);
            if (d is null) return NotFound();

            // altes File löschen
            SafeDelete(d.FilePath);
            var fullPath = SaveFile(file);

            d.FilePath = fullPath;
            d.OriginalFileName = file.FileName;
            d.ContentType = file.ContentType;
            d.FileSize = file.Length;

            await _repo.UpdateAsync(d, ct);
            await _repo.SaveChangesAsync(ct);

            return NoContent();
        }

        /// DELETE /api/v1/documents/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            var d = await _repo.GetAsync(id, ct);
            if (d is null) return NoContent();

            if (!string.IsNullOrWhiteSpace(d.FilePath) && System.IO.File.Exists(d.FilePath))
            {
                SafeDelete(d.FilePath);
            }
            await _repo.DeleteAsync(id, ct);
            await _repo.SaveChangesAsync(ct);
            return NoContent();
        }

        [HttpDelete("{id:guid}/file")]
        public async Task<IActionResult> DeleteFile(Guid id, CancellationToken ct)
        {
            var d = await _repo.GetAsync(id, ct);
            if (d is null) return NotFound();

            if (!string.IsNullOrWhiteSpace(d.FilePath) && System.IO.File.Exists(d.FilePath))
            {
                SafeDelete(d.FilePath);
            }

            d.FilePath = null;
            d.OriginalFileName = null;
            d.ContentType = null;
            d.FileSize = null;

            await _repo.UpdateAsync(d, ct);
            await _repo.SaveChangesAsync(ct);

            return NoContent();
        }
    }
}
