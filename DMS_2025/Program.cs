using DMS_2025.DAL.Context;
using DMS_2025.DAL.Repositories.EfCore;
using DMS_2025.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// controllers + swagger (optional)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// conn string (Development from env)
var cs = builder.Configuration.GetConnectionString("Default")
         ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
         ?? "Host=localhost;Port=5432;Database=dms_db;Username=postgres;Password=postgres";

builder.Services.AddDbContext<DmsDbContext>(opt => opt.UseNpgsql(cs));

// repos
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
    db.Database.Migrate(); // makes sure schema is up to date
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "OK");

app.MapGet("/db-ping", async (DmsDbContext db) =>
{
    var ok = await db.Database.CanConnectAsync();
    return Results.Ok(new { canConnect = ok });
});

app.Run();
