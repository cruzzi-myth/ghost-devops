using GhostDevOps.Gateway.Models;

namespace GhostDevOps.Gateway.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  BrainService — calls the Python LangGraph Brain (LangServe endpoint).
//  This is the bridge between the .NET Gateway and the Python AI agents.
// ─────────────────────────────────────────────────────────────────────────────

public class BrainService(IHttpClientFactory factory, ILogger<BrainService> logger)
{
    private readonly HttpClient _http = factory.CreateClient("GhostBrain");

    public async Task<BrainOutput?> AnalyzeAndFixAsync(
        string issueDescription,
        string logs,
        string service,
        CancellationToken ct = default)
    {
        logger.LogInformation("Calling Ghost Brain for service: {Service}", service);

        var request = new BrainInvokeRequest(new BrainInput(issueDescription, logs, service));

        try
        {
            var response = await _http.PostAsJsonAsync(
                "/ghost-devops/invoke",
                request,
                cancellationToken: ct
            );

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<BrainInvokeResponse>(
                cancellationToken: ct
            );

            if (result?.Output is null)
            {
                logger.LogWarning("Brain returned empty output for {Service}", service);
                return null;
            }

            logger.LogInformation(
                "Brain analysis complete. Safe: {IsSafe}, Iterations: {Iterations}",
                result.Output.IsSafe,
                result.Output.Iterations
            );

            return result.Output;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reach Ghost Brain at {Url}", _http.BaseAddress);
            return null;
        }
    }
}
