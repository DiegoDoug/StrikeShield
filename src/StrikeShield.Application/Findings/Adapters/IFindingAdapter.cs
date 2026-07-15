using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Findings.Adapters;

public record AdapterParseResult(IReadOnlyList<Finding> Findings, IReadOnlyList<Asset> Assets);

/// <summary>
/// Normalizes one tool's native output format into the internal Finding/
/// Asset shape (docs/ARCHITECTURE.md §6). One adapter per native format,
/// not per tool — <see cref="SarifFindingAdapter"/> alone covers Strix/
/// Semgrep/CodeQL/Trivy once those land (Phase 4), since they all emit
/// SARIF 2.1.0 already.
/// </summary>
public interface IFindingAdapter
{
    /// <summary>Matched case-insensitively against PlaybookStep.ToolName.</summary>
    string ToolName { get; }

    AdapterParseResult Parse(string rawContent, Guid scanJobId, Guid stepRunId, Target target);
}
