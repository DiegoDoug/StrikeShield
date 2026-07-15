namespace StrikeShield.Domain.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Target> Targets { get; set; } = new List<Target>();
    public ICollection<Engagement> Engagements { get; set; } = new List<Engagement>();
}
