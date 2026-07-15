namespace StrikeShield.Domain.Entities;

/// <summary>
/// One tool invocation within a Playbook. The Orchestrator launches this
/// as an isolated Docker container per docs/ARCHITECTURE.md §5:
/// "{ImageRepository}:{ImageTag}" pinned image, ArgsTemplate with
/// "{target}"/"{output}" placeholders substituted at run time, bounded by
/// TimeoutSeconds/MemoryLimitBytes/NanoCpus.
/// </summary>
public class PlaybookStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlaybookId { get; set; }
    public Playbook? Playbook { get; set; }

    public int Order { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ImageRepository { get; set; } = string.Empty;
    public string ImageTag { get; set; } = string.Empty;

    /// <summary>
    /// Space-delimited CLI args with "{target}"/"{output}" placeholders
    /// (e.g. "-u {target} -jsonl -o {output}"). Phase 2 keeps this a plain
    /// template rather than a structured arg list — fine while targets/
    /// output paths never contain spaces, which holds for URLs/domains/IPs.
    /// </summary>
    public string ArgsTemplate { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 300;
    public long MemoryLimitBytes { get; set; } = 512L * 1024 * 1024;
    public long NanoCpus { get; set; } = 1_000_000_000L;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<StepRun> StepRuns { get; set; } = new List<StepRun>();
}
