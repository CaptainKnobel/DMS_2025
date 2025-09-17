using DMS_2025.DAL.Abstractions;    // IDocumentRepository  // haben wir das?
using DMS_2025.DAL.Entities;        // Document (Entity)    // ka ... ich hab mal irgendwas abgetippt, wird sicher ein tolles refactoring
using DMS_2025.REST.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DMS_2025.REST.Controllers.V1
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repo;
        public DocumentsController(IDocumentRepository repo) => _repo = repo;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DocumentResponse>>> GetAll(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? q = null,
            [FromQuery] string? tag = null,
            [FromQuery] string? sort = null,
            CancellationToken ct = default)
        {
            var (items, total) = await _repo.SearchAsync(page, pageSize, q, tag, sort, ct);
            Response.Headers["X-Total-Count"] = total.ToString();

            var result = items.Select(d => new DocumentResponse
            {
                Id = d.Id,
                FileName = d.FileName,
                ContentType = d.ContentType,
                SizeBytes = d.SizeBytes,
                Title = d.Title,
                Tags = d.Tags,
                CreatedUtc = d.CreatedUtc
            });

            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DocumentResponse>> Get(Guid id, CancellationToken ct)
        {
            var d = await _repo.GetAsync(id, ct);
            return d is null
                ? NotFound()
                : Ok(new DocumentResponse
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    ContentType = d.ContentType,
                    SizeBytes = d.SizeBytes,
                    Title = d.Title,
                    Tags = d.Tags,
                    CreatedUtc = d.CreatedUtc
                });
        }

        [HttpPost]
        public async Task<ActionResult<DocumentResponse>> Create([FromBody] DocumentCreateRequest req, CancellationToken ct)
        {
            var entity = new Document
            {
                Id = Guid.NewGuid(),
                FileName = req.FileName,
                ContentType = req.ContentType,
                SizeBytes = req.SizeBytes,
                Title = req.Title,
                Tags = req.Tags,
                CreatedUtc = DateTime.UtcNow
            };

            await _repo.AddAsync(entity, ct);

            var dto = new DocumentResponse
            {
                Id = entity.Id,
                FileName = entity.FileName,
                ContentType = entity.ContentType,
                SizeBytes = entity.SizeBytes,
                Title = entity.Title,
                Tags = entity.Tags,
                CreatedUtc = entity.CreatedUtc
            };

            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] DocumentUpdateRequest req, CancellationToken ct)
        {
            var d = await _repo.GetAsync(id, ct);
            if (d is null) return NotFound();

            d.Title = req.Title ?? d.Title;
            d.Tags = req.Tags ?? d.Tags;

            await _repo.UpdateAsync(d, ct);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _repo.DeleteAsync(id, ct);
            return NoContent();
        }
    }
}
