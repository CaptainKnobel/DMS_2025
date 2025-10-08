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
using Serilog;
using DMS_2025.DAL;
using DMS_2025.REST.Messaging;
using RabbitMQ.Client;

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
builder.Services.AddSwaggerGen();
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

// ----- RabbitMQ -----
builder.Services.AddSingleton(sp =>
{
    var uri = builder.Configuration["RabbitMQ:Uri"]
        ?? Environment.GetEnvironmentVariable("RABBITMQ__URI")
        ?? "amqp://guest:guest@rabbitmq:5672";
    var factory = new ConnectionFactory {
        Uri = new Uri(uri),
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled = true,                     // (re-declare queues, QoS, consumer)
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        RequestedHeartbeat = TimeSpan.FromSeconds(30),      // hilft Timeouts
        ClientProvidedName = "dms_2025-rest"                // schöner im RMQ UI
    };
    return factory.CreateConnection();
});
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

// ----- build
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
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

app.UseExceptionHandler(); // exception handler, because you alone can't handle my exceptional programming skills
app.UseStatusCodePages();
//app.UseHttpsRedirection();  // only ok for local

//app.UseAuthorization();

app.MapControllers();
//app.MapGet("/", () => "OK");

//app.MapGet("/db-ping", async (DmsDbContext db) =>
//{
//    var ok = await db.Database.CanConnectAsync();
//    return Results.Ok(new { canConnect = ok });
//});

//app.MapGet("/api/v1/documents/{id}", async (Guid id, IDocumentRepository repo, CancellationToken ct) =>
//{
//    var doc = await repo.GetAsync(id, ct);
//    return doc is null ? Results.NotFound() : Results.Ok(doc);
//});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })); // i'm sick rn so it'd be good if at least one of us is healthy ...

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