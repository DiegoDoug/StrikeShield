namespace StrikeShield.Domain.Enums;

/// <summary>
/// A PlaybookAmendment (docs/PHASED_PLAN.md Phase 6, the Adaptive Planner)
/// is always a proposal, never applied automatically — the Orchestrator
/// pauses the ScanJob (StepRunStatus/ScanJobStatus.AwaitingApproval) once
/// one targets a not-yet-run step, and only Approved/Rejected lets
/// execution continue.
/// </summary>
public enum PlaybookAmendmentStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
