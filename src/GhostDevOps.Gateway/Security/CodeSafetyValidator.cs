namespace GhostDevOps.Gateway.Security;

// ─────────────────────────────────────────────────────────────────────────────
//  The "Least Privilege Agency" Security Sandbox.
//
//  Before ANY AI-generated code touches the repository, it passes through this
//  validator. This is the "Senior Flex" — you never let an AI have unchecked
//  write access to production infrastructure.
//
//  Two layers of defense:
//    1. Pattern-based blocklist (fast, catches obvious sabotage)
//    2. Risk scoring (determines whether HITL approval is required)
// ─────────────────────────────────────────────────────────────────────────────

public class CodeSafetyValidator
{
    // ── Absolute blocklist — any match = instant rejection ───────────────────
    private static readonly string[] BlockedPatterns =
    [
        "rm -rf",
        "DROP TABLE",
        "DROP DATABASE",
        "TRUNCATE",
        "DELETE FROM",       // Only blocked without WHERE clause — see IsHighRisk
        "format c:",
        "sudo",
        "chmod 777",
        "ProcessStartInfo",  // No spawning shell processes from AI code
        "Assembly.Load",     // No dynamic assembly loading
        "Environment.Exit",
        "__import__('os')",  // Python shell escape
        "eval(",
        "exec(",
    ];

    // ── High-risk patterns — require HITL approval ────────────────────────────
    private static readonly string[] HighRiskPatterns =
    [
        "HttpClient",         // External network calls
        "File.Delete",
        "Directory.Delete",
        "Environment.GetEnvironmentVariable",
        "SqlCommand",         // Raw SQL (should use parameterized queries)
        "Process.Start",
        "WebClient",
        "System.Net",
    ];

    public ValidationResult Validate(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new ValidationResult(false, true, "Code is empty", 0);

        // ── Layer 1: Hard blocklist ──────────────────────────────────────────
        foreach (var pattern in BlockedPatterns)
        {
            if (code.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(
                    IsSafe: false,
                    IsHighRisk: true,
                    Reason: $"BLOCKED: Dangerous pattern detected → '{pattern}'",
                    RiskScore: 100
                );
            }
        }

        // ── Layer 2: Risk scoring ────────────────────────────────────────────
        int riskScore = 0;
        var matchedRisks = new List<string>();

        foreach (var pattern in HighRiskPatterns)
        {
            if (code.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                riskScore += 20;
                matchedRisks.Add(pattern);
            }
        }

        // Reward good patterns (reduce risk score)
        if (code.Contains("using var",   StringComparison.OrdinalIgnoreCase)) riskScore -= 10;
        if (code.Contains("CancellationToken",  StringComparison.OrdinalIgnoreCase)) riskScore -= 10;
        if (code.Contains("await ",       StringComparison.OrdinalIgnoreCase)) riskScore -= 5;

        riskScore = Math.Max(0, Math.Min(riskScore, 100));

        bool isHighRisk = riskScore >= 40 || matchedRisks.Count > 0;
        string reason = matchedRisks.Count > 0
            ? $"High-risk patterns found: {string.Join(", ", matchedRisks)} — requires human approval"
            : "Code passed all safety checks";

        return new ValidationResult(
            IsSafe:    true,
            IsHighRisk: isHighRisk,
            Reason:    reason,
            RiskScore: riskScore
        );
    }
}

public record ValidationResult(
    bool   IsSafe,
    bool   IsHighRisk,
    string Reason,
    int    RiskScore
);
