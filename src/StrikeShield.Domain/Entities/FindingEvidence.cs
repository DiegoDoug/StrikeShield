namespace StrikeShield.Domain.Entities;

/// <summary>
/// A single piece of supporting evidence for a Finding — a raw HTTP
/// request/response, a log excerpt, a screenshot reference, etc. Kept
/// generic (Type + Content) since adapters differ wildly in what they can
/// supply (docs/ARCHITECTURE.md §6).
/// </summary>
public class FindingEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FindingId { get; set; }
    public Finding? Finding { get; set; }

    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
