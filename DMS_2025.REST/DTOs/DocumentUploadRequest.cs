using Microsoft.AspNetCore.Http;

namespace DMS_2025.REST.DTOs
{
    public sealed class DocumentUploadRequest
    {
        public IFormFile File { get; set; } = default!;
        public string? Title { get; set; }
        public string? Location { get; set; }
        public DateTime? CreationDate { get; set; }
        public string? Author { get; set; }
    }
}
