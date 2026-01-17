using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public sealed class ElasticSettings
{
    public string Scheme { get; set; } = "http";
    public string Host { get; set; } = "elasticsearch";
    public int Port { get; set; } = 9200;
    public string DocumentsIndex { get; set; } = "documents";
}
