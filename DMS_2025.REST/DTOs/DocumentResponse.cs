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

        // read-only Infos für UI
        public bool HasFile { get; set; }
        public long? FileSize { get; set; }
        public string? OriginalFileName { get; set; }
    }
}
