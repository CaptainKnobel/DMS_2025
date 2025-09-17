using System.ComponentModel.DataAnnotations;

namespace DMS_2025.REST.DTOs
{
    public class DocumentResponse
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Location { get; set; }
        public DateTime? CreationDate { get; set; }
        public string? Author { get; set; }
    }
}
