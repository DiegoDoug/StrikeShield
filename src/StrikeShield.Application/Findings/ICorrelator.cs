namespace StrikeShield.Application.Findings;

public interface ICorrelator
{
    /// <summary>
    /// Groups all of a ScanJob's Findings that share an exact
    /// DedupeFingerprint into one CorrelationGroup each (docs/ARCHITECTURE.md
    /// §6). Deterministic only — the LLM-assisted escalation path for
    /// ambiguous cross-tool matches that don't share a fingerprint is
    /// Phase 6, not here. Returns the number of CorrelationGroups created.
    /// </summary>
    Task<int> CorrelateAsync(Guid scanJobId, CancellationToken cancellationToken = default);
}
