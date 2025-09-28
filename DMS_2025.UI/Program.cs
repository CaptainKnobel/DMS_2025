using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// CORS (Für den Fall, dass UI unter anderem Origin läuft als /api):
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
));

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.All
});

app.UseCors();

// Statische Dateien ausliefern (wwwroot)
app.UseDefaultFiles();  // bedient index.html automatisch
app.UseStaticFiles();

// Fallback für SPA-Routing:
app.MapFallbackToFile("index.html");

app.Run();
