namespace StrikeShield.Domain.Entities;

/// <summary>
/// A named, ordered sequence of tool steps. Phase 2 only ever seeds one
/// ("nuclei-quick", a single step), but the schema already supports the
/// multi-step DAGs Phase 5 adds (PlaybookStep.Order is a simple sequence
/// for now; dependsOn/condition fields land with the DAG executor).
/// </summary>
public class Playbook
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable, URL/API-friendly identifier (e.g. "nuclei-quick").</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<PlaybookStep> Steps { get; set; } = new List<PlaybookStep>();
    public ICollection<ScanJob> ScanJobs { get; set; } = new List<ScanJob>();
}
