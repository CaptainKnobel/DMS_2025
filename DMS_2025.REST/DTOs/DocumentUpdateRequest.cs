using System.ComponentModel.DataAnnotations;

namespace DMS_2025.REST.DTOs
{
    public class DocumentUpdateRequest
    {
        [MaxLength(255)]
        public string? Title { get; set; }
        [MaxLength(1024)]
        public string? Tags { get; set; }
    }
}
