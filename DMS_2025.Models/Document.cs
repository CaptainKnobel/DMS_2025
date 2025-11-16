using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMS_2025.Models
{
    public class Document
    {
        [Required]
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public string? Location { get; set; }           // Human-friendly Ablage- oder Entstehungsort (fachlich), NICHT der Datei-Pfad.
        public string? Summary { get; set; }            // AI summary

        // metadata
        public DateTime? CreationDate { get; set; }
        public string? Author { get; set; }
        public string? FilePath { get; set; }           // absolute path im Container/Host
        public string? OriginalFileName { get; set; }   // z.B. "scan.pdf"
        public string? ContentType { get; set; }        // z.B. "application/pdf"
        public long? FileSize { get; set; }             // Bytes
    }
}
