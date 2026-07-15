namespace StrikeShield.Domain.Enums;

public enum StepRunStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    TimedOut = 4,

    /// <summary>
    /// Never launched: at least one of this step's DependsOn steps didn't
    /// reach Completed and the step's Condition is OnSuccess (the default)
    /// — see docs/PHASED_PLAN.md Phase 5.
    /// </summary>
    Skipped = 5
}
