using StrikeShield.Domain.Enums;

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

    /// <summary>
    /// Fallback ordering hint and topological-sort tie-breaker (Phase 5's
    /// PlaybookDagPlanner) — DependsOn is authoritative for execution
    /// order once a step declares any.
    /// </summary>
    public int Order { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ImageRepository { get; set; } = string.Empty;
    public string ImageTag { get; set; } = string.Empty;

    /// <summary>
    /// Stable identifier for this step within its Playbook, used by other
    /// steps' DependsOn (e.g. "subfinder", "katana") — stable across
    /// re-seeding, unlike Id.
    /// </summary>
    public string StepKey { get; set; } = string.Empty;

    /// <summary>
    /// StepKeys of steps that must finish before this one is considered
    /// (docs/PHASED_PLAN.md Phase 5). Empty means "no dependencies, run in
    /// Order". PlaybookDagPlanner resolves these into a topological order.
    /// </summary>
    public List<string> DependsOn { get; set; } = new();

    /// <summary>Whether this step still runs if a DependsOn step didn't complete successfully.</summary>
    public StepCondition Condition { get; set; } = StepCondition.OnSuccess;

    /// <summary>
    /// Space-delimited CLI args with "{target}"/"{output}" placeholders
    /// (e.g. "-u {target} -jsonl -o {output}"). Phase 2 keeps this a plain
    /// template rather than a structured arg list — fine while targets/
    /// output paths never contain spaces, which holds for URLs/domains/IPs.
    /// Phase 5 adds "{assetsFile}"/"{assetsFile:&lt;AssetType&gt;}" tokens,
    /// expanded by StepArgsBuilder into a path to a newline-delimited file
    /// of the values this step's dependencies discovered, written into the
    /// same shared scan-output volume as "{output}".
    /// </summary>
    public string ArgsTemplate { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 300;
    public long MemoryLimitBytes { get; set; } = 512L * 1024 * 1024;
    public long NanoCpus { get; set; } = 1_000_000_000L;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<StepRun> StepRuns { get; set; } = new List<StepRun>();
}
