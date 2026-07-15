using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Reporting;

/// <summary>Maps the API's kebab-case route segment to/from ReportType.</summary>
public static class ReportTypeParser
{
    public static bool TryParse(string value, out ReportType type)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "executive":
                type = ReportType.Executive;
                return true;
            case "technical":
                type = ReportType.Technical;
                return true;
            case "dev-remediation":
                type = ReportType.DevRemediation;
                return true;
            case "compliance":
                type = ReportType.Compliance;
                return true;
            default:
                type = default;
                return false;
        }
    }

    public static string ToRouteSegment(ReportType type) => type switch
    {
        ReportType.Executive => "executive",
        ReportType.Technical => "technical",
        ReportType.DevRemediation => "dev-remediation",
        ReportType.Compliance => "compliance",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
