using System.Buffers;
using System.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

GC.RegisterForFullGCNotification(10, 10);
_ = Task.Run(async () =>
{
    while (true)
    {
        var status = GC.WaitForFullGCApproach(millisecondsTimeout: 500);
        if (status == GCNotificationStatus.Succeeded)
        {
            var gcInfo = GC.GetGCMemoryInfo();
            var usedPercent = gcInfo.HeapSizeBytes == 0
                ? 0
                : (double)gcInfo.MemoryLoadBytes / gcInfo.TotalAvailableMemoryBytes * 100;

            if (usedPercent >= 80)
            {
                logger.LogWarning(
                    "Memory pressure warning: {UsedPercent:F1}% of available memory in use. " +
                    "HeapSize={HeapSizeBytes} LOH may be under stress.",
                    usedPercent,
                    gcInfo.HeapSizeBytes);
            }

            GC.CancelFullGCNotification();
            GC.RegisterForFullGCNotification(10, 10);
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
    }
});

app.MapPost("/process", async (HttpContext context, CancellationToken ct) =>
{
    const int bufferSize = 10 * 1024 * 1024; // 10 MB

    var pool = ArrayPool<byte>.Shared;
    var buffer = pool.Rent(bufferSize);
    try
    {
        // Simulate processing work using the rented buffer instead of
        // allocating a new byte[] that would land on the LOH.
        buffer[0] = 0xFF;
        buffer[bufferSize - 1] = 0xFF;

        await Task.Delay(TimeSpan.FromMilliseconds(50), ct);

        var bytesRead = await context.Request.Body.ReadAsync(
            buffer.AsMemory(0, bufferSize), ct);

        logger.LogInformation("Processed request, read {BytesRead} bytes.", bytesRead);

        await context.Response.WriteAsync("OK", ct);
    }
    finally
    {
        pool.Return(buffer, clearArray: true);
    }
});

app.MapGet("/health", async (HttpContext context, CancellationToken ct) =>
{
    var gcInfo = GC.GetGCMemoryInfo();
    var lohSize = GC.GetGeneration(new object()) >= 0
        ? GC.GetTotalMemory(forceCollection: false)
        : 0L;

    await context.Response.WriteAsJsonAsync(new
    {
        status = "healthy",
        totalManagedMemoryBytes = GC.GetTotalMemory(forceCollection: false),
        heapSizeBytes = gcInfo.HeapSizeBytes,
        memoryLoadBytes = gcInfo.MemoryLoadBytes,
        totalAvailableMemoryBytes = gcInfo.TotalAvailableMemoryBytes,
    }, ct);
});

await app.RunAsync();