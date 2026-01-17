using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMS_2025.Services.Worker.Elastic
{
    public sealed class DocumentIndexItem
    {
        public Guid Id { get; set; }
        public string TextContent { get; set; } = string.Empty;
    }
}
