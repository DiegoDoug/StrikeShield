using StrikeShield.Domain.Enums;

namespace StrikeShield.Domain.Entities;

/// <summary>
/// A single playbook run request. Stubbed in Phase 1 — it only records
/// intent (Queued) once the scope gate passes. Real Docker-orchestrated
/// execution lands in Phase 2.
/// </summary>
public class ScanJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EngagementId { get; set; }
    public Engagement? Engagement { get; set; }

    public Guid TargetId { get; set; }
    public Target? Target { get; set; }

    public string PlaybookName { get; set; } = string.Empty;
    public ScanJobStatus Status { get; set; } = ScanJobStatus.Queued;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
