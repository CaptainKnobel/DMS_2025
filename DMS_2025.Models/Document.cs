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
        public string? Location { get; set; }

        // metadata
        public TimeSpan? CreationDate { get; set; }
        public string? Author { get; set; }
    }
}
