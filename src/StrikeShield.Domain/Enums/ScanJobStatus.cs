namespace StrikeShield.Domain.Enums;

public enum ScanJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    TimedOut = 4,

    /// <summary>
    /// Paused: the Adaptive Planner (docs/PHASED_PLAN.md Phase 6) proposed
    /// a PlaybookAmendment targeting the next not-yet-run step, and it's
    /// still Pending. Reverts to Queued (so the Orchestrator resumes it)
    /// the moment a human approves or rejects that amendment.
    /// </summary>
    AwaitingApproval = 5
}
