// ─────────────────────────────────────────────────────────────────────────────
//  GhostDevOps.TargetApp — The intentionally broken service.
//  This app simulates three real-world defects:
//    1. /leak       → Memory leak (unclosed byte array accumulation)
//    2. /slow-query → Simulates a slow DB query without proper cancellation
//    3. /metrics    → Prometheus scrape endpoint (healthy telemetry)
//
//  Ghost DevOps monitors this container and autonomously patches the bugs.
// ─────────────────────────────────────────────────────────────────────────────

using Prometheus;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseHttpMetrics(); // Prometheus HTTP metrics middleware

// ── Health & Prometheus endpoints ────────────────────────────────────────────
app.MapHealthChecks("/health");
app.MapMetrics("/metrics");   // Prometheus scrapes this

// ─────────────────────────────────────────────────────────────────────────────
//  BUG #1: Memory Leak
//  Root cause: Static list accumulates 10MB byte arrays on every request.
//  The `leak` list is never cleared — this is what Ghost DevOps must detect
//  and fix by suggesting a bounded collection or a /clear endpoint.
// ─────────────────────────────────────────────────────────────────────────────
var leak = new List<byte[]>();  // BUG: Should be bounded or cleared

app.MapGet("/leak", () =>
{
    leak.Add(new byte[10 * 1024 * 1024]); // 10MB per call
    var memMb = leak.Count * 10;
    Console.WriteLine($"[LEAK] Simulated {memMb}MB allocated. Objects: {leak.Count}");
    return Results.Ok(new
    {
        message  = "Memory leaked!",
        totalMb  = memMb,
        objects  = leak.Count
    });
});

// ─────────────────────────────────────────────────────────────────────────────
//  BUG #2: Slow Query (Thread Pool Exhaustion)
//  Root cause: Task.Run without ConfigureAwait + no CancellationToken.
//  This blocks thread pool threads and causes latency spikes under load.
//  Ghost DevOps Architect identifies this; Developer adds CancellationToken.
// ─────────────────────────────────────────────────────────────────────────────
app.MapGet("/slow-query", async () =>
{
    // BUG: Thread.Sleep inside Task.Run blocks a thread pool thread.
    // FIX (Ghost DevOps will suggest): await Task.Delay(ct) with CancellationToken
    await Task.Run(() => Thread.Sleep(3000));
    return Results.Ok(new { message = "Slow query completed", latencyMs = 3000 });
});

// ─────────────────────────────────────────────────────────────────────────────
//  BUG #3: Unclosed SQL Connection simulation
//  Root cause: SqlConnection opened without a `using` statement.
//  In a real app this exhausts the connection pool after ~100 requests.
// ─────────────────────────────────────────────────────────────────────────────
app.MapGet("/sql-leak", () =>
{
    // BUG: Simulates a SqlConnection opened but never disposed.
    // Ghost DevOps Developer will wrap this in a `using` statement.
    SimulateSqlConnectionLeak();
    return Results.Ok(new { message = "SQL connection opened (and leaked!)" });
});

app.MapGet("/", () => Results.Ok(new
{
    service  = "GhostDevOps TargetApp",
    version  = "1.0.0-broken",
    bugs     = new[] { "/leak", "/slow-query", "/sql-leak" },
    health   = "/health",
    metrics  = "/metrics"
}));

app.Run();

// ─────────────────────────────────────────────────────────────────────────────
static void SimulateSqlConnectionLeak()
{
    // Intentionally NOT using `using` — this is the bug Ghost DevOps detects.
    // var conn = new SqlConnection(connectionString);
    // conn.Open();
    // Real fix: using var conn = new SqlConnection(connectionString); { ... }
    Console.WriteLine("[SQL-LEAK] SqlConnection opened without disposal.");
    Thread.Sleep(100); // Simulate DB round-trip
}
