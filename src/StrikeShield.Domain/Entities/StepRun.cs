using StrikeShield.Domain.Enums;

namespace StrikeShield.Domain.Entities;

/// <summary>
/// One executed instance of a PlaybookStep for a given ScanJob — one
/// container lifecycle, tracked so "docker ps -a" always has a matching
/// row here (and vice versa: nothing should be running that isn't tracked).
/// </summary>
public class StepRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ScanJobId { get; set; }
    public ScanJob? ScanJob { get; set; }

    public Guid PlaybookStepId { get; set; }
    public PlaybookStep? PlaybookStep { get; set; }

    public StepRunStatus Status { get; set; } = StepRunStatus.Pending;
    public string? ContainerId { get; set; }
    public long? ExitCode { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Artifact> Artifacts { get; set; } = new List<Artifact>();
}
