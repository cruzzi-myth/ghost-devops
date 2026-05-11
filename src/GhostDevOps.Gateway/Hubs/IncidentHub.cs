using Microsoft.AspNetCore.SignalR;
using GhostDevOps.Gateway.Models;

namespace GhostDevOps.Gateway.Hubs;

// ─────────────────────────────────────────────────────────────────────────────
//  SignalR Hub — real-time pipeline between the Ghost Gateway and the React
//  Command Center dashboard. All dashboard updates flow through here.
//
//  Client events (dashboard → server):
//    ApproveProposal(incidentId)  — human approves the AI fix
//    RejectProposal(incidentId)   — human rejects the AI fix
//
//  Server events (server → dashboard):
//    IncidentDetected(incident)   — new alert came in
//    AnalysisStarted(incidentId)  — LangGraph brain is running
//    NewMonologueEntry(entry)     — live inner-monologue update from agents
//    ProposalReady(incident)      — brain returned a fix, awaiting HITL
//    PRCreated(incident)          — PR was pushed to GitHub
//    IncidentResolved(incidentId) — Prometheus fired "resolved"
// ─────────────────────────────────────────────────────────────────────────────

public class IncidentHub : Hub
{
    private readonly ILogger<IncidentHub> _logger;

    public IncidentHub(ILogger<IncidentHub> logger) => _logger = logger;

    // ── HITL: Human approves the AI's proposed fix ───────────────────────────
    public async Task ApproveProposal(string incidentId)
    {
        _logger.LogInformation("HITL Approval received for incident {Id}", incidentId);
        // Broadcast to all other dashboard clients and signal the gateway service
        await Clients.Others.SendAsync("ProposalApproved", incidentId);
        // The gateway picks this up via IHubContext in AlertController
    }

    // ── HITL: Human rejects the AI's proposed fix ────────────────────────────
    public async Task RejectProposal(string incidentId, string reason)
    {
        _logger.LogWarning("HITL Rejection for incident {Id}: {Reason}", incidentId, reason);
        await Clients.Others.SendAsync("ProposalRejected", new { incidentId, reason });
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Dashboard client connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Dashboard client disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}

// ── Inner monologue DTO sent to the React dashboard ──────────────────────────
public record MonologueEntry(
    string Agent,     // "Architect" | "Developer" | "Validator"
    string Message,
    DateTime Timestamp
);
