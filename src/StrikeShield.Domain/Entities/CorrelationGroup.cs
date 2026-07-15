namespace StrikeShield.Domain.Entities;

/// <summary>
/// Many Findings (possibly from different tools within the same ScanJob)
/// that represent the same underlying issue, deduped by an exact
/// <see cref="Finding.DedupeFingerprint"/> match (docs/ARCHITECTURE.md §6,
/// Phase 3). The LLM-assisted escalation path for ambiguous cross-tool
/// matches that don't share an exact fingerprint arrives in Phase 6.
/// </summary>
public class CorrelationGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Finding> Findings { get; set; } = new List<Finding>();
}
