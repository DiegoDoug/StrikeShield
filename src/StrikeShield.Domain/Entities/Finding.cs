using StrikeShield.Domain.Enums;

namespace StrikeShield.Domain.Entities;

/// <summary>
/// The unified shape every tool adapter normalizes into (docs/ARCHITECTURE.md
/// §6) — this is what makes cross-tool correlation (Phase 3/6) and AI
/// reporting (Phase 7) possible without every later phase re-parsing each
/// tool's native output format.
/// </summary>
public class Finding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ScanJobId { get; set; }
    public ScanJob? ScanJob { get; set; }

    public Guid StepRunId { get; set; }
    public StepRun? StepRun { get; set; }

    public Guid? CorrelationGroupId { get; set; }
    public CorrelationGroup? CorrelationGroup { get; set; }

    public string SourceTool { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public FindingSeverity Severity { get; set; } = FindingSeverity.Info;

    public string? CvssVector { get; set; }
    public double? CvssScore { get; set; }
    public List<string> CweIds { get; set; } = new();
    public List<string> CveIds { get; set; } = new();
    public string? OwaspCategory { get; set; }
    public List<string> MitreAttackTechniques { get; set; } = new();

    /// <summary>
    /// Host/URL/file/line/port the finding was observed at — the raw value
    /// used to compute <see cref="DedupeFingerprint"/>'s location component
    /// (see Findings/FindingFingerprint.cs).
    /// </summary>
    public string AffectedAsset { get; set; } = string.Empty;

    public string? PocCode { get; set; }
    public List<string> ReproSteps { get; set; } = new();
    public string? RecommendedFix { get; set; }
    public List<string> VerificationSteps { get; set; } = new();

    public FindingStatus Status { get; set; } = FindingStatus.New;

    /// <summary>
    /// Stable hash of (normalizedTarget, cweId ?? cveId ?? templateId,
    /// normalizedLocation) — see Findings/FindingFingerprint.cs. The
    /// Correlator groups on this deterministically; the LLM-assisted path
    /// for ambiguous cross-tool matches is Phase 6, not here.
    /// </summary>
    public string DedupeFingerprint { get; set; } = string.Empty;

    public double? Confidence { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<FindingEvidence> Evidence { get; set; } = new List<FindingEvidence>();
}
