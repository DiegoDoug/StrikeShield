using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Reporting;

/// <summary>
/// One finding as it appears in a rendered report. Every field except
/// BusinessImpact/RiskRating is sourced directly from the normalized
/// Finding/FindingEvidence entities (docs/ARCHITECTURE.md §6) — never
/// invented by the LLM, so a report can never show a hallucinated CVSS
/// score or CWE id. BusinessImpact/RiskRating are the two narrative
/// fields the Reporting Agent's single LLM call supplies per finding;
/// ReportGenerationService refuses to render if either is missing for
/// any finding (docs/PHASED_PLAN.md Phase 7's schema-validation
/// acceptance criterion).
/// </summary>
public record ReportFindingPayload(
    Guid FindingId,
    string Title,
    string? Description,
    string SourceTool,
    FindingSeverity Severity,
    string? CvssVector,
    double? CvssScore,
    IReadOnlyList<string> CweIds,
    IReadOnlyList<string> CveIds,
    string? OwaspCategory,
    IReadOnlyList<string> MitreAttackTechniques,
    string AffectedAsset,
    IReadOnlyList<string> ReproSteps,
    IReadOnlyList<string> EvidenceRefs,
    string? PocCode,
    string? RecommendedFix,
    IReadOnlyList<string> VerificationSteps,
    string BusinessImpact,
    string RiskRating);
