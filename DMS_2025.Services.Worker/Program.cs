// See https://aka.ms/new-console-template for more information
using DMS_2025.Services.Worker.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ImageMagick;
using Tesseract;
using Serilog;
using System;
using System.Text;
using System.Text.Json;
using DMS_2025.DAL.Context;
using Microsoft.EntityFrameworkCore;
using DMS_2025.Services.Worker.GenAI;

await Host.CreateDefaultBuilder(args)
    .UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console())
    .ConfigureServices((ctx, services) =>
    {
        var uri = ctx.Configuration["RabbitMQ:Uri"]
            ?? Environment.GetEnvironmentVariable("RABBITMQ__URI")
            ?? "amqp://guest:guest@rabbitmq:5672";
        var factory = new ConnectionFactory {
            Uri = new Uri(uri),
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,                     // (re-declare queues, QoS, consumer)
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(30),      // hilft Timeouts
            ClientProvidedName = "dms_2025-worker"              // schöner im RMQ UI
        };
        services.AddSingleton(factory);                 // nur Factory registrieren

        // MinIO Settings from ENV / Configuration binding (MINIO__* mapped on "Minio:*")
        services.Configure<MinioSettings>(ctx.Configuration.GetSection("Minio"));

        // register MinIO Client
        services.AddSingleton<IMinioClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<MinioSettings>>().Value;
            return new MinioClient()
                .WithEndpoint(cfg.Endpoint)
                .WithCredentials(cfg.AccessKey, cfg.SecretKey)
                .WithSSL(cfg.UseSSL)
                .Build();
        });

        // DbContext 
        var connStr = ctx.Configuration.GetConnectionString("Default")
                  ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                  ?? "Host=postgres;Port=5432;Database=dms_db;Username=postgres;Password=postgres";

        services.AddDbContext<DmsDbContext>(opt =>
        {
            opt.UseNpgsql(connStr);
        });

        // GeminiService for calling Gemini API
        services.AddSingleton<GeminiService>();

        // register QueueConsumer
        services.AddHostedService<QueueConsumer>();
    })
    .RunConsoleAsync();

public class QueueConsumer : BackgroundService
{
    private readonly ConnectionFactory _factory;
    private IConnection? _conn;
    private IModel? _ch;
    private readonly string _queue;
    private readonly ILogger<QueueConsumer> _log;
    private readonly int _demoDelayMs;
    private readonly IMinioClient _minio;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GeminiService _gemini;

    public QueueConsumer(ConnectionFactory factory, IConfiguration cfg, ILogger<QueueConsumer> log, IMinioClient minio, IServiceScopeFactory scopeFactory,
    GeminiService gemini)
    {
        _factory = factory;
        _queue = cfg["RabbitMQ:Queue"]
            ?? Environment.GetEnvironmentVariable("RABBITMQ__QUEUE")
            ?? "documents";
        _demoDelayMs = int.TryParse(cfg["WORKER_DEMO_DELAY_MS"], out var ms) ? ms : 0;
        _log = log;
        _minio = minio;
        _scopeFactory = scopeFactory;
        _gemini = gemini;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Lazy connect + kleiner Retry
        for (var attempt = 1; _conn is null || !_conn.IsOpen; attempt++)
        {
            try
            {
                _conn = _factory.CreateConnection();
                _ch = _conn.CreateModel();
            }
            catch (Exception ex) when (attempt < 15 && !stoppingToken.IsCancellationRequested)
            {
                _log.LogWarning(ex, "RabbitMQ noch nicht bereit (Versuch {Attempt}). Retry in 2s …", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        var ch = _ch!;

        _conn!.ConnectionShutdown += (_, ea) =>
            _log.LogWarning("RMQ connection shutdown: {ReplyText} ({Code})", ea.ReplyText, ea.ReplyCode);
        ch.ModelShutdown += (_, ea) =>
            _log.LogWarning("RMQ channel shutdown: {ReplyText} ({Code})", ea.ReplyText, ea.ReplyCode);

        ch.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);
        ch.BasicQos(0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(ch);
        consumer.Registered += (_, __) => { _log.LogInformation("RMQ consumer registered"); return Task.CompletedTask; };
        consumer.Unregistered += (_, __) => { _log.LogInformation("RMQ consumer unregistered"); return Task.CompletedTask; };
        consumer.Shutdown += (_, ea) => { _log.LogWarning("RMQ consumer shutdown: {ReplyText} ({Code})", ea.ReplyText, ea.ReplyCode); return Task.CompletedTask; };

        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;

                Guid? documentId = null;
                if (root.TryGetProperty("documentId", out var docIdElement)
                    && docIdElement.ValueKind == JsonValueKind.String
                    && Guid.TryParse(docIdElement.GetString(), out var parsed))
                {
                    documentId = parsed;
                }

                if (string.Equals(type, "OcrRequested", StringComparison.OrdinalIgnoreCase))
                {
                    var bucket = root.GetProperty("bucket").GetString()!;
                    var objectName = root.GetProperty("objectName").GetString()!;
                    var original = root.GetProperty("originalFileName").GetString();

                    var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
                    await using (var fs = File.Create(tmp))
                    {
                        await _minio.GetObjectAsync(new GetObjectArgs()
                            .WithBucket(bucket)
                            .WithObject(objectName)
                            .WithCallbackStream(s => s.CopyTo(fs)), cancellationToken: stoppingToken);
                    }

                    var mode = Environment.GetEnvironmentVariable("OCR_MODE")?.ToLowerInvariant() ?? "magick+tess";

                    string text = mode switch
                    {
                        "magick+tess" => await OcrWithMagickAndTesseractAsync(tmp, stoppingToken),
                        "tesswrapper" => await OcrWithTesseractWrapperAsync(tmp, stoppingToken),
                        "cli" => await OcrWithCliFallbackAsync(tmp, stoppingToken),
                        _ => await OcrWithMagickAndTesseractAsync(tmp, stoppingToken)
                    };

                    _log.LogInformation("OCR({Mode}) for {Original}: {Preview}...", mode, original, text.Length > 200 ? text[..200] : text);

                    // GenAI + DB update
                    if (documentId.HasValue)
                    {
                        try
                        {
                            _log.LogInformation("Requesting GenAI summary for document {DocId}", documentId);

                            var summary = await _gemini.SummarizeAsync(text, stoppingToken);
                            if (!string.IsNullOrWhiteSpace(summary))
                            {
                                using var scope = _scopeFactory.CreateScope();
                                var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();

                                var entity = await db.Documents
                                    .FirstOrDefaultAsync(d => d.Id == documentId.Value, stoppingToken);

                                if (entity is null)
                                {
                                    _log.LogWarning("Document {DocId} not found when trying to store summary", documentId);
                                }
                                else
                                {
                                    entity.Summary = summary;
                                    await db.SaveChangesAsync(stoppingToken);
                                    _log.LogInformation("Stored summary for document {DocId}", documentId);
                                }
                            }
                            else
                            {
                                _log.LogWarning("GenAI returned empty summary for document {DocId}", documentId);
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Error while generating/storing summary for document {DocId}", documentId);
                        }
                    }
                    else
                    {
                        _log.LogWarning("OCR message had no documentId; skipping summary generation");
                    }
                }
                else
                {
                    _log.LogInformation("Ignoring message type {Type}", type ?? "<unknown>");
                }

                ch.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Error while processing message");
                ch.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        var tag = ch.BasicConsume(_queue, autoAck: false, consumer);
        _log.LogInformation("BasicConsume started. ConsumerTag: {Tag}", tag);

        // Auf Shutdown warten
        var tcs = new TaskCompletionSource<object?>();
        stoppingToken.Register(() =>
        {
            try { ch.Close(); } catch { }
            try { ch.Dispose(); } catch { }
            try { _conn?.Close(); } catch { }
            try { _conn?.Dispose(); } catch { }
            tcs.TrySetResult(null);
        });
        await tcs.Task;
    }
    private static async Task<string> RunTesseractAsync(string pdfPath, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "tesseract",
            Arguments = $"\"{pdfPath}\" stdout -l deu+eng",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var p = System.Diagnostics.Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync(ct);
        var err = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0) throw new Exception("tesseract failed: " + err);
        return output;
    }

    // Tesseract stuff
    static async Task<string> OcrWithMagickAndTesseractAsync(string pdfPath, CancellationToken ct)
    {
        // PDF -> IMages (300 DPI) via Magick.NET (Ghostscript intern)
        var images = new MagickImageCollection();
        var readSettings = new MagickReadSettings
        {
            Density = new Density(300, 300), // 300 DPI
            Format = MagickFormat.Pdf
        };
        await Task.Run(() => images.Read(pdfPath, readSettings), ct);

        var sb = new System.Text.StringBuilder();

        // Tesseract .NET Wrapper initialisieren (deu+eng)
        using var engine = new TesseractEngine(
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX") ?? "/usr/share/tesseract-ocr/4.00/tessdata",
            "deu+eng",
            EngineMode.Default);

        // Seiten schlau vorverarbeiten und OCR'n
        for (int i = 0; i < images.Count; i++)
        {
            using var img = (MagickImage)images[i];
            // ein paar Filter:
            img.ColorSpace = ColorSpace.Gray;
            img.ContrastStretch(new Percentage(0.5), new Percentage(99.5)); // Leichte Normalisierung
            img.Deskew(new Percentage(40));                                 // Gerade ziehen, wenn leicht schief
            img.FilterType = FilterType.Triangle;
            img.Resize(new Percentage(110));                                // minimal größer

            // temporär als PNG speichern, weil der Tesseract-Wrapper gut mit Files kann
            var tmpPng = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}.png");
            await img.WriteAsync(tmpPng, MagickFormat.Png, ct);

            using var pix = Pix.LoadFromFile(tmpPng);
            using var page = engine.Process(pix);
            sb.AppendLine(page.GetText());

            try { File.Delete(tmpPng); } catch { /* ignore */ }
        }

        images.Dispose();
        return sb.ToString();
    }

    static async Task<string> OcrWithTesseractWrapperAsync(string imageOrPdfPath, CancellationToken ct)
    {
        // Erwartet bereits Images (PNG/TIFF). Wenn es PDF ist, vorher extern rastern!
        using var engine = new TesseractEngine(
            Environment.GetEnvironmentVariable("TESSDATA_PREFIX") ?? "/usr/share/tesseract-ocr/4.00/tessdata",
            "deu+eng",
            EngineMode.Default);

        // Wenn PDF -> kurze Ausnahme, damit klar ist, warum es fehlschlägt
        if (Path.GetExtension(imageOrPdfPath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("OCR_MODE=tesswrapper erwartet Rasterbilder (PNG/TIFF), nicht PDF.");

        using var pix = Pix.LoadFromFile(imageOrPdfPath);
        using var page = engine.Process(pix);
        return await Task.FromResult(page.GetText());
    }

    static async Task<string> OcrWithCliFallbackAsync(string pdfPath, CancellationToken ct)
    {
        // Ghostscript: PDF -> PNGs
        var outDir = Path.Combine(Path.GetTempPath(), "ocr_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);

        var gs = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "gs",
            Arguments = $"-q -dBATCH -dNOPAUSE -sDEVICE=png16m -r300 -sOutputFile=\"{Path.Combine(outDir, "page-%04d.png")}\" \"{pdfPath}\"",
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using (var p = System.Diagnostics.Process.Start(gs)!)
        {
            _ = await p.StandardOutput.ReadToEndAsync(ct);
            var err = await p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            if (p.ExitCode != 0) throw new Exception("Ghostscript failed: " + err);
        }

        var sb = new System.Text.StringBuilder();
        foreach (var img in Directory.EnumerateFiles(outDir, "page-*.png").OrderBy(x => x))
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "tesseract",
                Arguments = $"--tessdata-dir /usr/share/tesseract-ocr/5/tessdata \"{img}\" stdout -l deu+eng", // $"\"{img}\" stdout -l deu+eng",
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var tp = System.Diagnostics.Process.Start(psi)!;
            var pageText = await tp.StandardOutput.ReadToEndAsync(ct);
            var terr = await tp.StandardError.ReadToEndAsync(ct);
            await tp.WaitForExitAsync(ct);
            if (tp.ExitCode != 0) throw new Exception("tesseract failed: " + terr);

            sb.AppendLine(pageText);
        }

        try { Directory.Delete(outDir, recursive: true); } catch { }
        return sb.ToString();
    }
}