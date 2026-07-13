using StrikeShield.Domain.Enums;

namespace StrikeShield.Domain.Entities;

public class Target
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public TargetType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ScanJob> ScanJobs { get; set; } = new List<ScanJob>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
