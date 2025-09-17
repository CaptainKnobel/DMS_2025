using System.ComponentModel.DataAnnotations;

namespace DMS_2025.REST.DTOs
{
    public class DocumentCreateRequest
    {
        // Im Model ist Title nicht [Required]; für S1 lasse ich es optional.
        [MaxLength(255)]
        public string? Title { get; set; }

        [MaxLength(255)]
        public string? Location { get; set; }

        public DateTime? CreationDate { get; set; }

        [MaxLength(255)]
        public string? Author { get; set; }
    }
}
