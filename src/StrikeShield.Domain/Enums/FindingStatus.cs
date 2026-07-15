namespace StrikeShield.Domain.Enums;

public enum FindingStatus
{
    New = 0,
    Confirmed = 1,
    FalsePositive = 2,
    Fixed = 3,
    AcceptedRisk = 4,
    Regressed = 5
}
