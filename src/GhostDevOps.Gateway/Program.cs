using GhostDevOps.Gateway.Controllers;
using GhostDevOps.Gateway.Hubs;
using GhostDevOps.Gateway.Security;
using GhostDevOps.Gateway.Services;
using Microsoft.EntityFrameworkCore;

// ─────────────────────────────────────────────────────────────────────────────
//  Ghost DevOps Gateway — .NET 9 Minimal API entry point
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR for real-time dashboard updates
builder.Services.AddSignalR();

// CORS for the React dashboard
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(
        "http://localhost:3000",   // local dev
        "http://ghost-dashboard"   // docker compose
    )
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials()          // Required for SignalR
));

// EF Core in-memory DB (swap for PostgreSQL in production)
builder.Services.AddDbContext<GhostDbContext>(o =>
    o.UseInMemoryDatabase("GhostDevOps"));

// HTTP client to the Python Brain
builder.Services.AddHttpClient("GhostBrain", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Brain:Url"] ?? "http://ghost-brain:8000");
    c.Timeout     = TimeSpan.FromSeconds(120); // LLM calls can take time
});

// Domain services
builder.Services.AddScoped<BrainService>();
builder.Services.AddScoped<GitHubService>();
builder.Services.AddScoped<KubernetesService>();
builder.Services.AddSingleton<CodeSafetyValidator>();

builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRouting();
app.MapControllers();
app.MapHub<IncidentHub>("/hubs/incidents");   // SignalR hub
app.MapHealthChecks("/health");

app.Logger.LogInformation("🚀 Ghost DevOps Gateway is running");
app.Logger.LogInformation("📡 Listening for Prometheus alerts at /api/alerts/receive");
app.Logger.LogInformation("🔌 SignalR hub at /hubs/incidents");

app.Run();
