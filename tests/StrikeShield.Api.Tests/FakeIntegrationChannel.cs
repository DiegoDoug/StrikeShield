using StrikeShield.Application.Notifications;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Deterministic IIntegrationChannel test double — records calls instead
/// of making a real HTTP request, so NotificationDispatcherTests.cs can
/// prove the fan-out logic without live Slack/webhook/GitHub endpoints.
/// </summary>
public class FakeIntegrationChannel : IIntegrationChannel
{
    public FakeIntegrationChannel(IntegrationType type)
    {
        Type = type;
    }

    public IntegrationType Type { get; }

    public int ScanCompletedCallCount { get; private set; }
    public int CriticalFindingsCallCount { get; private set; }
    public IReadOnlyList<CriticalFindingNotification>? LastCriticalFindings { get; private set; }

    public Task SendScanCompletedAsync(
        Integration integration,
        ScanCompletionNotification notification,
        CancellationToken cancellationToken = default)
    {
        ScanCompletedCallCount++;
        return Task.CompletedTask;
    }

    public Task SendCriticalFindingsAsync(
        Integration integration,
        ScanCompletionNotification notification,
        IReadOnlyList<CriticalFindingNotification> findings,
        CancellationToken cancellationToken = default)
    {
        CriticalFindingsCallCount++;
        LastCriticalFindings = findings;
        return Task.CompletedTask;
    }
}
