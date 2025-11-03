//using System.Reflection;
//using System.Text.Json;
//using System.Text.Json.Serialization;
//using DMS_2025.DAL;
using DMS_2025.DAL.Context;                 // DmsDbContext
using DMS_2025.DAL.Repositories.Interfaces; // IDocumentRepository
using DMS_2025.DAL.Repositories.EfCore;     // DocumentRepository
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using DMS_2025.REST.Validation;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using DMS_2025.DAL;
using DMS_2025.REST;
using DMS_2025.REST.Messaging;
using DMS_2025.REST.Config;
using RabbitMQ.Client;
using Microsoft.Extensions.Options;
using Minio;
using Microsoft.OpenApi.Models;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// - Logging
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers(options =>
{
    options.Filters.Add<FluentValidationActionFilter>();   // <- add our filter once
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DMS_2025.REST", Version = "v1" });
    c.CustomSchemaIds(t => t.FullName);
});
//builder.Services.AddValidatorsFromAssemblyContaining<DocumentCreateValidator>();
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddProblemDetails();
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 20L * 1024 * 1024; // 20 MB
});
// DAL (DbContext + Repos, incl. ConnectionString)
// DbContext + Repo (Runtime-Registration)
var cs = builder.Configuration.GetConnectionString("Default")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? "Host=localhost;Port=5432;Database=dms_db;Username=postgres;Password=postgres";
builder.Services.AddDbContext<DmsDbContext>(opt =>
    opt.UseNpgsql(cs, o => o.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(2),
        errorCodesToAdd: null
        )
    )
);
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

// CORS nur für Dev: erlaube UI-Ursprünge
builder.Services.AddCors(o => o.AddPolicy("Dev", p =>
    p.SetIsOriginAllowed(origin =>
    {
        if (string.IsNullOrWhiteSpace(origin)) return false;
        try { return new Uri(origin).IsLoopback; } catch { return false; }
    })
    .AllowAnyHeader()
    .AllowAnyMethod()
));

// ----- RabbitMQ -----
builder.Services.AddSingleton<IConnectionFactory>(sp =>
{
    var uri = builder.Configuration["RabbitMQ:Uri"]
              ?? Environment.GetEnvironmentVariable("RABBITMQ__URI")
              ?? "amqp://guest:guest@rabbitmq:5672";
    return new ConnectionFactory
    {
        Uri = new Uri(uri),
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled = true,                     // (re-declare queues, QoS, consumer)
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        RequestedHeartbeat = TimeSpan.FromSeconds(30),      // hilft Timeouts
        ClientProvidedName = "dms_2025-rest"                // schöner im RMQ UI
    };
});
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

// ----- MinIO -----
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("Minio"));
builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var cfg = sp.GetRequiredService<IOptions<MinioSettings>>().Value;
    return new MinioClient()
        .WithEndpoint(cfg.Endpoint)
        .WithCredentials(cfg.AccessKey, cfg.SecretKey)
        .WithSSL(cfg.UseSSL)
        .Build();
});

// --- File storage root (configurable) ---
var uploadRoot = builder.Configuration["FileStorage:Root"]
    ?? Path.Combine(Path.GetTempPath(), "dms_uploads");
Directory.CreateDirectory(uploadRoot);
builder.Services.AddSingleton(new UploadRoot(uploadRoot));

// --- Nginx als Proxy zulassen ---
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    // Erlaubte default Proxies/Netze leeren (sonst wird geblockt)
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();

    // Wie viele Proxies maximal in der Kette (Wir haben 1: Nginx)
    options.ForwardLimit = 1;

    // feste Nginx-IP explizit eintragen (Docker-Bridge-Netz (172.18.0.0/16))
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("172.18.0.0"), 16));
});

// =====----- build -----=====
var app = builder.Build();

app.UseForwardedHeaders(/*new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
}*/);
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // PathBase vom Reverse Proxy akzeptieren (X-Forwarded-Prefix)
    /*
    app.Use((ctx, next) =>
    {
        var prefix = ctx.Request.Headers["X-Forwarded-Prefix"].FirstOrDefault();
        if (!string.IsNullOrEmpty(prefix))
            ctx.Request.PathBase = prefix; // z.B. /swagger (nur UI), /api (falls du je umhängst)
        return next();
    });
    */
    // Swagger "servers" dynamisch aufbauen (Scheme/Host vom Proxy; API-Basis aus Header)
    /*app.UseSwagger(c =>
    {
        c.PreSerializeFilters.Add((swagger, httpReq) =>
        {
            try
            {
                var scheme = httpReq.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? httpReq.Scheme ?? "http";
                var host = httpReq.Headers["X-Forwarded-Host"].FirstOrDefault() ?? httpReq.Host.Value ?? "localhost";
                var externalApiBase = httpReq.Headers["X-External-Api-Base"].FirstOrDefault() ?? string.Empty;
                swagger.Servers = new List<Microsoft.OpenApi.Models.OpenApiServer>
            {
                new() { Url = $"{scheme}://{host}{externalApiBase}" }
            };
            }
            catch {
                // ignore
            }
        });
    });
    */
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
    for (var attempt = 1; attempt <= 10; attempt++)
    {
        try
        {
            db.Database.Migrate();  // makes sure schema is up to date
            log.LogInformation("DB migration succeeded on attempt {Attempt}.", attempt);
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            log.LogWarning(ex, "DB migration failed (attempt {Attempt}/10). Retrying in 2s…", attempt);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Dev");         // CORS einschalten (muss vor MapControllers passieren)
app.MapControllers();
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "Healthy", ok = true}));

app.Run();
// ^ v !only one! v ^
//await app.RunAsync();




/*
Submission - Sprint 1: Project-Setup, REST API, DAL (Due: Wednesday, 17 September 2025, 11:59 PM) Assignment
Submission - Sprint 2: Web-UI (Due: Monday, 29 September 2025, 2:00 PM) Assignment
Submission - Sprint 3: Queuing (Due: Wednesday, 8 October 2025, 11:59 PM) Assignment
Submission - Sprint 4: Workers, MinIO, OCR (Due: Monday, 3 November 2025, 2:00 PM) Assignment
Submission - Sprint 5: GenAI (Due: Monday, 17 November 2025, 2:00 PM) Assignment
Submission - Sprint 6: ELK, Use Cases (Monday, 15 December 2025, 2:00 PM) Assignment
Submission - Sprint 7: Integration-Test, Batch (Due: Sunday, 18 January 2026, 11:59 PM) 
*/