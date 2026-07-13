namespace StrikeShield.Domain.Enums;

public enum ScanJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    TimedOut = 4
}
