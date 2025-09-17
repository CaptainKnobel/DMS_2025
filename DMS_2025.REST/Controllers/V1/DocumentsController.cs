using DMS_2025.DAL.Repositories.Interfaces; // IDocumentRepository
using DMS_2025.Models;                       // Document (Entity)
using DMS_2025.REST.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMS_2025.REST.Controllers.V1
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _repo;
        public DocumentsController(IDocumentRepository repo) => _repo = repo;

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

            var result = items.Select(d => new DocumentResponse
            {
                Id = d.Id,
                Title = d.Title,
                Location = d.Location,
                CreationDate = d.CreationDate,
                Author = d.Author
            });

            return Ok(result);
        }

        /// GET /api/v1/documents/{id}
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<DocumentResponse>> Get(Guid id, CancellationToken ct)
        {
            var d = await _repo.GetAsync(id, ct);
            return d is null
                ? NotFound()
                : Ok(new DocumentResponse
                {
                    Id = d.Id,
                    Title = d.Title,
                    Location = d.Location,
                    CreationDate = d.CreationDate,
                    Author = d.Author
                });
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

            var dto = new DocumentResponse
            {
                Id = entity.Id,
                Title = entity.Title,
                Location = entity.Location,
                CreationDate = entity.CreationDate,
                Author = entity.Author
            };

            return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
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

        /// DELETE /api/v1/documents/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            await _repo.DeleteAsync(id, ct);
            await _repo.SaveChangesAsync(ct); // WICHTIG
            return NoContent();
        }
    }
}
