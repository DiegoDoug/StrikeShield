using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Playbooks;

public interface IAdaptivePlanner
{
    /// <summary>
    /// After a StepRun completes and discovers new Assets, may propose a
    /// PlaybookAmendment targeting one of the ScanJob's not-yet-run
    /// remaining steps (docs/ARCHITECTURE.md §3, docs/PHASED_PLAN.md
    /// Phase 6) — e.g. a tech-fingerprint-driven nuclei tag addition.
    /// Never applies anything itself; the amendment is created Pending and
    /// only takes effect once a human approves it via
    /// IPlaybookAmendmentService. A no-op (no LLM call at all) if no BYOK
    /// key is configured, the same contract Strix has with no LLM_API_KEY.
    /// </summary>
    Task ProposeAmendmentAsync(
        Guid scanJobId,
        Guid completedStepRunId,
        IReadOnlyList<PlaybookStep> remainingSteps,
        CancellationToken cancellationToken = default);
}
