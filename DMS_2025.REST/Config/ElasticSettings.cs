namespace DMS_2025.REST.Elastic
{
    public class ElasticSettings
    {
        public string? Scheme { get; set; } = "http";
        public string? Host { get; set; } = "elasticsearch";
        public int Port { get; set; } = 9200;
        public string? IndexDocuments { get; set; } = "documents";
    }
}
