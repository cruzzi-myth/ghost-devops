namespace GhostDevOps.Gateway.Models;

// ─────────────────────────────────────────────────────────────────────────────
//  Response from the Python LangGraph Brain (LangServe /ghost-devops/invoke).
//  Maps to the AgentState TypedDict returned by the graph.
// ─────────────────────────────────────────────────────────────────────────────

public record BrainInvokeRequest(BrainInput Input);

public record BrainInput(
    string IssueDescription,
    string Logs,
    string Service
);

public record BrainInvokeResponse(BrainOutput Output);

public record BrainOutput(
    string  Plan,           // Architect's diagnosis
    string  CodeFix,        // Developer's code patch
    bool    IsSafe,         // Safety validator result
    string  ArchitectLog,   // Full Architect reasoning (for Inner Monologue UI)
    string  DeveloperLog,   // Full Developer reasoning (for Inner Monologue UI)
    int     Iterations      // How many debate rounds occurred
);
