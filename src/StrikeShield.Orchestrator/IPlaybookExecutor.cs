using StrikeShield.Domain.Entities;

namespace StrikeShield.Orchestrator;

public interface IPlaybookExecutor
{
    /// <summary>
    /// Runs every step of <paramref name="scanJob"/>.Playbook in order,
    /// each as an isolated Docker container, recording a StepRun (and any
    /// collected Artifact) per step. Updates scanJob.Status to
    /// Completed/Failed as a final step; never throws for expected
    /// container-level failures (a failed/timed-out step just stops the
    /// run and marks the ScanJob Failed) — only for genuinely unexpected
    /// errors (e.g. the Docker daemon being unreachable).
    /// </summary>
    Task ExecuteAsync(ScanJob scanJob, CancellationToken cancellationToken);
}
