using StrikeShield.Domain.Enums;

namespace StrikeShield.Domain.Entities;

/// <summary>
/// A single playbook run request against a Target, gated by its
/// Engagement's approval/scope window (see Engagement.CheckAuthorizedForScan).
/// Phase 2 wires this up to real Docker-orchestrated execution: the
/// Orchestrator worker polls for Queued jobs and runs each PlaybookStep as
/// an isolated container, recording progress as StepRun rows.
/// </summary>
public class ScanJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EngagementId { get; set; }
    public Engagement? Engagement { get; set; }

    public Guid TargetId { get; set; }
    public Target? Target { get; set; }

    public Guid PlaybookId { get; set; }
    public Playbook? Playbook { get; set; }

    public ScanJobStatus Status { get; set; } = ScanJobStatus.Queued;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<StepRun> StepRuns { get; set; } = new List<StepRun>();
}
