namespace GhostDevOps.Gateway.Models;

// ─────────────────────────────────────────────────────────────────────────────
//  Represents a Ghost DevOps incident from detection through resolution.
//  Stored in-memory (swap for PostgreSQL in production).
// ─────────────────────────────────────────────────────────────────────────────

public class Incident
{
    public Guid     Id            { get; set; } = Guid.NewGuid();
    public string   Fingerprint   { get; set; } = string.Empty;  // Prometheus fingerprint
    public string   AlertName     { get; set; } = string.Empty;
    public string   Service       { get; set; } = string.Empty;
    public string   Summary       { get; set; } = string.Empty;
    public string   Status        { get; set; } = "Detected";    // Detected | Analyzing | FixProposed | PRCreated | Verified Fixed
    public DateTime DetectedAt    { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt   { get; set; }
    public string?  RootCause     { get; set; }                  // Architect's analysis
    public string?  ProposedFix   { get; set; }                  // Developer's code fix
    public string?  PullRequestUrl{ get; set; }
    public bool     HumanApproved { get; set; } = false;
    public bool     IsHighRisk    { get; set; } = false;

    // DORA metrics helpers
    public double? MttrMinutes => ResolvedAt.HasValue
        ? (ResolvedAt.Value - DetectedAt).TotalMinutes
        : null;
}

// ─────────────────────────────────────────────────────────────────────────────
//  EF Core DbContext for incident persistence
// ─────────────────────────────────────────────────────────────────────────────
using Microsoft.EntityFrameworkCore;

public class GhostDbContext(DbContextOptions<GhostDbContext> options) : DbContext(options)
{
    public DbSet<Incident> Incidents => Set<Incident>();
}
