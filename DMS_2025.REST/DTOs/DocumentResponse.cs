using System.ComponentModel.DataAnnotations;

namespace DMS_2025.REST.DTOs
{
    public class DocumentResponse
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = default!;
        public string ContentType { get; set; } = default!;
        public long SizeBytes { get; set; }
        public string? Title { get; set; }
        public string? Tags { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
