namespace StrikeShield.Domain.Enums;

/// <summary>
/// The four audience-specific document types the Reporting Agent produces
/// from one Engagement's correlated finding set (docs/ARCHITECTURE.md §7).
/// </summary>
public enum ReportType
{
    Executive = 0,
    Technical = 1,
    DevRemediation = 2,
    Compliance = 3
}
