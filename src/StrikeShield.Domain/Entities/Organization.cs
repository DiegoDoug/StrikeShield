namespace StrikeShield.Domain.Entities;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Client> Clients { get; set; } = new List<Client>();
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}
