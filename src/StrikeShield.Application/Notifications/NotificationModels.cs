using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Notifications;

public record ScanCompletionNotification(
    Guid ScanJobId,
    Guid EngagementId,
    string EngagementName,
    string TargetValue,
    string PlaybookName,
    ScanJobStatus Status,
    int FindingsCount,
    int CriticalFindingsCount);

public record CriticalFindingNotification(
    Guid FindingId,
    string Title,
    string? Description,
    string AffectedAsset,
    string SourceTool,
    double? CvssScore);
