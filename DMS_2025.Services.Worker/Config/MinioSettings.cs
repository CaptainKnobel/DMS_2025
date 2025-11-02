namespace DMS_2025.Services.Worker.Config
{
    public sealed class MinioSettings
    {
        public string Endpoint { get; set; } = "minio:9000";
        public string AccessKey { get; set; } = "minioadmin";
        public string SecretKey { get; set; } = "minioadmin";
        public string Bucket { get; set; } = "uploads";
        public bool UseSSL { get; set; } = false;
    }
}
