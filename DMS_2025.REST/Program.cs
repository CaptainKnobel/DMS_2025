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
//using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// - Logging
//builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

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
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default"); // ToDo: connection string fixen, der richtige müsste aus DAL kommen
builder.Services.AddDbContext<DmsDbContext>(opt => opt.UseNpgsql(cs));
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(); // exception handler, because you alone can't handle my exceptional programming skills
app.UseStatusCodePages();
app.UseHttpsRedirection();  // only ok for local

//app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })); // i'm sick rn so it'd be good if at least one of us is healthy ...

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DmsDbContext>();
    db.Database.Migrate();
}

app.Run();
