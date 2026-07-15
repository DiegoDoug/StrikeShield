namespace StrikeShield.Application.Findings;

public interface ICorrelator
{
    /// <summary>
    /// Groups all of a ScanJob's Findings that share an exact
    /// DedupeFingerprint into one CorrelationGroup each (docs/ARCHITECTURE.md
    /// §6) — always runs, never touches the LLM. Then, if an LLM client is
    /// configured (docs/PHASED_PLAN.md Phase 6), escalates the residual
    /// ambiguous cross-tool matches (same target/severity, no shared
    /// fingerprint) via one batched LLM call per cluster. Returns the total
    /// number of CorrelationGroups created by both passes.
    /// </summary>
    Task<int> CorrelateAsync(Guid scanJobId, CancellationToken cancellationToken = default);
}
