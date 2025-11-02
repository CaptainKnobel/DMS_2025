using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DMS_2025.Services.Worker.Config;

namespace DMS_2025.Services.Worker.OCR_Worker
{
    public sealed class OcrWorker : BackgroundService
    {
        private readonly ILogger<OcrWorker> _log;
        private readonly IMinioClient _minio;
        private readonly MinioSettings _m;
        private readonly IConfiguration _cfg;
        public OcrWorker(ILogger<OcrWorker> log, IMinioClient minio, IOptions<MinioSettings> m, IConfiguration cfg)
        { _log = log; _minio = minio; _m = m.Value; _cfg = cfg; }

        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            var uri = _cfg["RABBITMQ__URI"] ?? "amqp://guest:guest@rabbitmq:5672/"; // mit Slash
            var queue = _cfg["RABBITMQ__QUEUE"] ?? "documents";

            var factory = new ConnectionFactory { Uri = new Uri(uri) };
            using var conn = factory.CreateConnection();
            using var ch = conn.CreateModel();
            ch.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);

            var consumer = new EventingBasicConsumer(ch);
            consumer.Received += async (_, ea) =>
            {
                try
                {
                    var msg = JsonSerializer.Deserialize<OcrRequestMessage>(ea.Body.Span);
                    if (msg is null) { ch.BasicAck(ea.DeliveryTag, false); return; }

                    var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
                    await using (var fs = File.Create(tmp))
                    {
                        await _minio.GetObjectAsync(new GetObjectArgs()
                          .WithBucket(msg.Bucket)
                          .WithObject(msg.ObjectName)
                          .WithCallbackStream(s => s.CopyTo(fs)), ct);
                    }

                    var text = await RunTesseractAsync(tmp, ct);
                    _log.LogInformation("OCR {Doc} -> {Preview}...", msg.OriginalFileName, text.Length > 160 ? text[..160] : text);

                    ch.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "OCR failed");
                    ch.BasicNack(ea.DeliveryTag, false, requeue: false);
                }
            };
            ch.BasicConsume(queue, autoAck: false, consumer);
            await Task.Delay(Timeout.Infinite, ct);
        }

        private static async Task<string> RunTesseractAsync(string pdf, CancellationToken ct)
        {
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = "tesseract",
                Arguments = $"\"{pdf}\" stdout -l deu+eng",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })!;
            var output = await p.StandardOutput.ReadToEndAsync(ct);
            var err = await p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            if (p.ExitCode != 0) throw new Exception("tesseract failed: " + err);
            return output;
        }

        private record OcrRequestMessage(Guid DocumentId, string Bucket, string ObjectName, string OriginalFileName);
    }
}
