using StrikeShield.Domain.Enums;

namespace StrikeShield.Domain.Entities;

/// <summary>
/// A pending, human-approved diff to one not-yet-run PlaybookStep of a
/// specific ScanJob — the Adaptive Planner's output (docs/ARCHITECTURE.md
/// §3, docs/PHASED_PLAN.md Phase 6). Never applied automatically: the
/// Orchestrator only substitutes ProposedArgsTemplate for the target
/// step's own ArgsTemplate once Status is Approved, and the underlying
/// PlaybookStep template row is never mutated — this is scoped to one
/// ScanJob run, not a permanent change to the shared playbook.
/// </summary>
public class PlaybookAmendment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ScanJobId { get; set; }
    public ScanJob? ScanJob { get; set; }

    /// <summary>The recon-ish step whose discovered Assets triggered this proposal.</summary>
    public Guid ProposedByStepRunId { get; set; }
    public StepRun? ProposedByStepRun { get; set; }

    /// <summary>The not-yet-run step this amendment would apply to, if approved.</summary>
    public Guid TargetPlaybookStepId { get; set; }
    public PlaybookStep? TargetPlaybookStep { get; set; }

    /// <summary>The LLM's plain-language justification (e.g. "fingerprinted WordPress on 3 subdomains").</summary>
    public string Rationale { get; set; } = string.Empty;

    /// <summary>Full replacement ArgsTemplate for the target step, used only if Approved.</summary>
    public string ProposedArgsTemplate { get; set; } = string.Empty;

    public PlaybookAmendmentStatus Status { get; set; } = PlaybookAmendmentStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
}
