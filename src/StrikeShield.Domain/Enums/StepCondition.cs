namespace StrikeShield.Domain.Enums;

/// <summary>
/// Governs whether a PlaybookStep runs once its DependsOn steps have
/// finished (docs/PHASED_PLAN.md Phase 5). Evaluated per-step by the
/// Orchestrator's DAG executor against the StepRun status of each
/// dependency, not the ScanJob as a whole — one failed recon branch
/// shouldn't skip a step in an unrelated branch.
/// </summary>
public enum StepCondition
{
    /// <summary>Run only if every step in DependsOn reached StepRunStatus.Completed. Steps with no dependencies always run.</summary>
    OnSuccess = 0,

    /// <summary>Run regardless of dependency outcome, as long as they reached a terminal status.</summary>
    Always = 1
}
