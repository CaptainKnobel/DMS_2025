//using System.Reflection;
//using System.Text.Json;
//using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
//using Serilog;
using DMS_2025.DAL;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// - Logging
//builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails(); //n
//builder.Services.AddValidatorsFromAssemblyContaining<DocumentCreateValidator>();

// DAL (DbContext + Repos, incl. ConnectionString)
builder.Services.AddDal(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(); // exception handler, because you alone can't handle my exceptional programming skills

app.UseHttpsRedirection();

//app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" })); // i'm sick rn so it'd be good if at least one of us is healthy ...

app.Run();
