using System.ComponentModel.DataAnnotations;

namespace DMS_2025.REST.DTOs
{
    public class DocumentUpdateRequest
    {
        [MaxLength(255)]
        public string? Title { get; set; }
        [MaxLength(255)]
        public string? Location { get; set; }
        public DateTime? CreationDate { get; set; }
        [MaxLength(255)]
        public string? Author { get; set; }
    }
}
