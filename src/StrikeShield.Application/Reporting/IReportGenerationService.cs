using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Reporting;

public interface IReportGenerationService
{
    /// <summary>
    /// Generates (and persists) one audience-specific PDF report for an
    /// Engagement's correlated finding set (docs/PHASED_PLAN.md Phase 7).
    /// Always regenerates rather than returning a cached prior Report row.
    /// Throws AppValidationException if reporting isn't configured (no
    /// BYOK LLM key), there are no findings yet, or the Reporting Agent's
    /// LLM response doesn't cover every finding with the required
    /// businessImpact/riskRating narrative fields.
    /// </summary>
    Task<Report> GenerateAsync(Guid engagementId, ReportType type, CancellationToken cancellationToken = default);
}
