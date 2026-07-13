namespace StrikeShield.Domain.Entities;

/// <summary>
/// Raw output collected from a StepRun (e.g. Nuclei's JSON-lines results).
/// Stored inline in Postgres for Phase 2 since these are small text
/// outputs — larger binary artifacts (screenshots, PDFs) move to object
/// storage per docs/ARCHITECTURE.md §2/§3 once Phase 4+ needs them.
/// </summary>
public class Artifact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StepRunId { get; set; }
    public StepRun? StepRun { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/plain";
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
