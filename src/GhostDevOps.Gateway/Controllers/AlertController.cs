using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using GhostDevOps.Gateway.Hubs;
using GhostDevOps.Gateway.Models;
using GhostDevOps.Gateway.Security;
using GhostDevOps.Gateway.Services;

namespace GhostDevOps.Gateway.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
//  AlertController — the main orchestration hub.
//
//  Receive → Analyze → Validate → (HITL?) → Act → Verify
//
//  The full Ghost DevOps control loop runs inside HandleAlert().
// ─────────────────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/alerts")]
public class AlertController(
    BrainService                 brainService,
    GitHubService                githubService,
    KubernetesService            k8sService,
    CodeSafetyValidator          validator,
    GhostDbContext               db,
    IHubContext<IncidentHub>     hub,
    ILogger<AlertController>     logger) : ControllerBase
{
    // ── POST /api/alerts/receive — entry point from Prometheus Alertmanager ───
    [HttpPost("receive")]
    public async Task<IActionResult> HandleAlert(
        [FromBody] PrometheusAlert alert,
        CancellationToken ct)
    {
        logger.LogWarning("Alert received: [{Status}] {Summary}",
            alert.Status, alert.CommonAnnotations.Summary);

        // ── Handle "resolved" events (close the self-healing loop) ────────────
        if (alert.Status == "resolved")
            return await HandleResolved(alert, ct);

        // ── Handle "firing" events (kick off the agentic pipeline) ────────────
        foreach (var instance in alert.Alerts.Where(a => a.Status == "firing"))
            await ProcessFiringAlert(instance, alert, ct);

        return Accepted();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Core pipeline: one firing alert → one incident → one PR
    // ─────────────────────────────────────────────────────────────────────────
    private async Task ProcessFiringAlert(
        AlertInstance   instance,
        PrometheusAlert parent,
        CancellationToken ct)
    {
        // ── 1. Create incident record ─────────────────────────────────────────
        var incident = new Incident
        {
            Fingerprint = instance.Fingerprint,
            AlertName   = instance.Labels.GetValueOrDefault("alertname", "Unknown"),
            Service     = instance.Labels.GetValueOrDefault("service", "unknown"),
            Summary     = parent.CommonAnnotations.Summary,
            Status      = "Detected"
        };

        db.Incidents.Add(incident);
        await db.SaveChangesAsync(ct);

        // ── Notify dashboard: new incident detected ───────────────────────────
        await hub.Clients.All.SendAsync("IncidentDetected", incident, cancellationToken: ct);
        logger.LogInformation("Incident {Id} created for alert {Alert}", incident.Id, incident.AlertName);

        // ── 2. Call LangGraph Brain ───────────────────────────────────────────
        incident.Status = "Analyzing";
        await db.SaveChangesAsync(ct);
        await hub.Clients.All.SendAsync("AnalysisStarted", incident.Id, cancellationToken: ct);

        // Simulate pulling last 100 lines of logs (real: call ELK/Loki API)
        var logs = $"""
            [{DateTime.UtcNow:u}] WARN  Service-A — memory at 95%
            [container] System.OutOfMemoryException: Insufficient memory to continue.
            [container] at TargetApp.Program+<>c.b__0_0 in /app/Program.cs:line 32
            [container] Total GC heap: 512MB | Large Object Heap: 256MB
            [container] List<byte[]> leak count: {new Random().Next(50, 200)}
            """;

        var brainOutput = await brainService.AnalyzeAndFixAsync(
            incident.Summary, logs, incident.Service, ct);

        if (brainOutput is null)
        {
            logger.LogError("Brain returned null for incident {Id} — aborting", incident.Id);
            incident.Status = "Failed";
            await db.SaveChangesAsync(ct);
            return;
        }

        // ── Emit inner monologue entries to dashboard ─────────────────────────
        await hub.Clients.All.SendAsync("NewMonologueEntry",
            new MonologueEntry("Architect", brainOutput.ArchitectLog, DateTime.UtcNow), ct);
        await hub.Clients.All.SendAsync("NewMonologueEntry",
            new MonologueEntry("Developer", brainOutput.DeveloperLog, DateTime.UtcNow), ct);

        incident.RootCause   = brainOutput.Plan;
        incident.ProposedFix = brainOutput.CodeFix;

        // ── 3. Safety sandbox validation ─────────────────────────────────────
        var validation = validator.Validate(brainOutput.CodeFix);

        await hub.Clients.All.SendAsync("NewMonologueEntry",
            new MonologueEntry("Validator",
                $"[Risk Score: {validation.RiskScore}/100] {validation.Reason}",
                DateTime.UtcNow), ct);

        if (!validation.IsSafe)
        {
            logger.LogCritical(
                "Safety check FAILED for incident {Id}: {Reason}", incident.Id, validation.Reason);
            incident.Status = "Rejected - Safety Failure";
            await db.SaveChangesAsync(ct);
            return;
        }

        incident.IsHighRisk = validation.IsHighRisk;

        // ── 4. HITL gate for high-risk changes ───────────────────────────────
        if (validation.IsHighRisk)
        {
            incident.Status = "AwaitingApproval";
            await db.SaveChangesAsync(ct);
            await hub.Clients.All.SendAsync("ProposalReady", incident, cancellationToken: ct);

            logger.LogWarning(
                "High-risk change for incident {Id} — waiting for human approval (SignalR)", incident.Id);
            // The ApproveProposal SignalR method in IncidentHub will update and re-trigger
            return;
        }

        // ── 5. Auto-approve low-risk changes and create PR ────────────────────
        await CreatePRAndProceed(incident, brainOutput.CodeFix, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HITL approval endpoint — called by dashboard via SignalR ApproveProposal
    // ─────────────────────────────────────────────────────────────────────────
    [HttpPost("approve/{incidentId:guid}")]
    public async Task<IActionResult> ApproveIncident(Guid incidentId, CancellationToken ct)
    {
        var incident = await db.Incidents.FindAsync(new object[] { incidentId }, ct);
        if (incident is null) return NotFound();

        incident.HumanApproved = true;
        await db.SaveChangesAsync(ct);

        // Re-trigger the PR flow with the cached fix
        await CreatePRAndProceed(incident, incident.ProposedFix!, ct);
        return Ok();
    }

    // ─────────────────────────────────────────────────────────────────────────
    private async Task CreatePRAndProceed(
        Incident incident, string codeFix, CancellationToken ct)
    {
        var prUrl = await githubService.CreateAutoFixPRAsync(
            incident,
            targetFilePath: "src/GhostDevOps.TargetApp/Program.cs",
            newFileContent: codeFix,
            ct
        );

        incident.PullRequestUrl = prUrl;
        incident.Status         = prUrl is not null ? "PRCreated" : "Failed";
        await db.SaveChangesAsync(ct);

        await hub.Clients.All.SendAsync("PRCreated", incident, cancellationToken: ct);
        logger.LogInformation("PR created: {Url}", prUrl);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Resolved event — Prometheus confirmed memory dropped below threshold
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<IActionResult> HandleResolved(
        PrometheusAlert alert, CancellationToken ct)
    {
        foreach (var instance in alert.Alerts)
        {
            var incident = await db.Incidents
                .Where(i => i.Fingerprint == instance.Fingerprint)
                .OrderByDescending(i => i.DetectedAt)
                .FirstOrDefaultAsync(ct);

            if (incident is null) continue;

            incident.Status     = "Verified Fixed";
            incident.ResolvedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Incident {Id} resolved in {MTTR:F1} minutes", incident.Id, incident.MttrMinutes);

            await hub.Clients.All.SendAsync(
                "IncidentResolved", incident.Id, cancellationToken: ct);

            // Trigger Kubernetes rolling restart to pick up new image
            await k8sService.TriggerRollingRestartAsync(
                @namespace:      "default",
                deploymentName:  incident.Service,
                ct
            );
        }

        return Ok();
    }

    // ── GET /api/incidents — DORA metrics data for the dashboard ─────────────
    [HttpGet("/api/incidents")]
    public async Task<IActionResult> GetIncidents(CancellationToken ct)
    {
        var incidents = await db.Incidents
            .OrderByDescending(i => i.DetectedAt)
            .Take(50)
            .ToListAsync(ct);

        var resolved  = incidents.Where(i => i.ResolvedAt.HasValue).ToList();
        var dora = new
        {
            DeploymentFrequency = incidents.Count(i => i.Status == "PRCreated" &&
                                    i.DetectedAt.Date == DateTime.UtcNow.Date),
            AvgMttrMinutes = resolved.Count > 0
                ? resolved.Average(i => i.MttrMinutes!.Value)
                : 0,
            ChangeFailureRate = incidents.Count > 0
                ? (double)incidents.Count(i => i.Status.Contains("Rejected")) / incidents.Count * 100
                : 0,
            TotalIncidents  = incidents.Count,
            ResolvedToday   = resolved.Count(i => i.ResolvedAt?.Date == DateTime.UtcNow.Date)
        };

        return Ok(new { incidents, dora });
    }
}
