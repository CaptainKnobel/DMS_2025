using System.ComponentModel.DataAnnotations;

namespace DMS_2025.REST.DTOs
{
    public class DocumentCreateRequest
    {
        [Required, MaxLength(255)]
        public string FileName { get; set; } = default!;
        [Required, MaxLength(127)]
        public string ContentType { get; set; } = default!;
        [Range(0, long.MaxValue)]
        public long SizeBytes { get; set; }
        [MaxLength(255)]
        public string? Title { get; set; }
        [MaxLength(1024)]
        public string? Tags { get; set; }
    }
}
