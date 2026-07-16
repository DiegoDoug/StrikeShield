using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StrikeShield.Application.Notifications;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using StrikeShield.Infrastructure.Persistence;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 8 acceptance test (docs/PHASED_PLAN.md): "Slack + generic webhook
/// notifications on scan completion / new critical finding; GitHub issue
/// creation as an optional integration." Uses fake IIntegrationChannel
/// doubles (no real HTTP calls) to prove the fan-out logic: which
/// integrations get which events, organization scoping, and that GitHub
/// only ever gets the critical-finding event, never plain scan completion.
/// </summary>
public class NotificationDispatcherTests
{
    private static StrikeShieldDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StrikeShieldDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new StrikeShieldDbContext(options);
    }

    private static ScanJob SeedCompletedScanJob(StrikeShieldDbContext db, Guid organizationId, FindingSeverity? findingSeverity)
    {
        var client = new Client { OrganizationId = organizationId, Name = "Client" };
        var project = new Project { ClientId = client.Id, Name = "Project" };
        var engagement = new Engagement
        {
            ProjectId = project.Id,
            Name = "Engagement",
            ScopeStart = DateTimeOffset.UtcNow.AddDays(-1),
            ScopeEnd = DateTimeOffset.UtcNow.AddDays(7),
            AllowedScopeRules = new List<string>()
        };
        var target = new Target { ProjectId = project.Id, Type = TargetType.Url, Value = "http://example.test" };
        var playbook = new Playbook { Slug = "nuclei-quick", Name = "Nuclei Quick Scan" };
        var scanJob = new ScanJob
        {
            EngagementId = engagement.Id,
            TargetId = target.Id,
            PlaybookId = playbook.Id,
            Status = ScanJobStatus.Completed
        };

        db.Clients.Add(client);
        db.Projects.Add(project);
        db.Engagements.Add(engagement);
        db.Targets.Add(target);
        db.Playbooks.Add(playbook);
        db.ScanJobs.Add(scanJob);

        if (findingSeverity is not null)
        {
            db.Findings.Add(new Finding
            {
                ScanJobId = scanJob.Id,
                StepRunId = Guid.NewGuid(),
                SourceTool = "nuclei",
                Title = "Critical thing",
                Severity = findingSeverity.Value,
                AffectedAsset = "http://example.test/",
                DedupeFingerprint = Guid.NewGuid().ToString()
            });
        }

        db.SaveChanges();

        return scanJob;
    }

    [Fact]
    public async Task DispatchForCompletedScanJobAsync_NotifiesEnabledIntegrations_OnScanCompletion()
    {
        await using var db = CreateInMemoryDbContext();
        var organizationId = Guid.NewGuid();
        var scanJob = SeedCompletedScanJob(db, organizationId, findingSeverity: null);

        var slackChannel = new FakeIntegrationChannel(IntegrationType.Slack);
        var webhookChannel = new FakeIntegrationChannel(IntegrationType.Webhook);

        db.Integrations.Add(new Integration
        {
            OrganizationId = organizationId,
            Type = IntegrationType.Slack,
            WebhookUrl = "https://hooks.slack.test/x",
            NotifyOnScanCompletion = true,
            NotifyOnCriticalFinding = true
        });
        await db.SaveChangesAsync();

        var dispatcher = new NotificationDispatcher(
            db,
            new IIntegrationChannel[] { slackChannel, webhookChannel },
            NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchForCompletedScanJobAsync(scanJob.Id, CancellationToken.None);

        Assert.Equal(1, slackChannel.ScanCompletedCallCount);
        Assert.Equal(0, slackChannel.CriticalFindingsCallCount);
        Assert.Equal(0, webhookChannel.ScanCompletedCallCount);
    }

    [Fact]
    public async Task DispatchForCompletedScanJobAsync_SendsCriticalFindings_OnlyWhenCriticalFindingsExist()
    {
        await using var db = CreateInMemoryDbContext();
        var organizationId = Guid.NewGuid();
        var scanJob = SeedCompletedScanJob(db, organizationId, findingSeverity: FindingSeverity.Critical);

        var slackChannel = new FakeIntegrationChannel(IntegrationType.Slack);

        db.Integrations.Add(new Integration
        {
            OrganizationId = organizationId,
            Type = IntegrationType.Slack,
            WebhookUrl = "https://hooks.slack.test/x",
            NotifyOnScanCompletion = false,
            NotifyOnCriticalFinding = true
        });
        await db.SaveChangesAsync();

        var dispatcher = new NotificationDispatcher(
            db,
            new IIntegrationChannel[] { slackChannel },
            NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchForCompletedScanJobAsync(scanJob.Id, CancellationToken.None);

        Assert.Equal(0, slackChannel.ScanCompletedCallCount);
        Assert.Equal(1, slackChannel.CriticalFindingsCallCount);
        Assert.Single(slackChannel.LastCriticalFindings!);
    }

    [Fact]
    public async Task DispatchForCompletedScanJobAsync_NeverSendsScanCompletion_ToGitHubIntegrations()
    {
        await using var db = CreateInMemoryDbContext();
        var organizationId = Guid.NewGuid();
        var scanJob = SeedCompletedScanJob(db, organizationId, findingSeverity: FindingSeverity.Critical);

        var gitHubChannel = new FakeIntegrationChannel(IntegrationType.GitHub);

        db.Integrations.Add(new Integration
        {
            OrganizationId = organizationId,
            Type = IntegrationType.GitHub,
            GitHubRepository = "acme/strikeshield-findings",
            GitHubAccessToken = "ghp_test",
            NotifyOnScanCompletion = true,
            NotifyOnCriticalFinding = true
        });
        await db.SaveChangesAsync();

        var dispatcher = new NotificationDispatcher(
            db,
            new IIntegrationChannel[] { gitHubChannel },
            NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchForCompletedScanJobAsync(scanJob.Id, CancellationToken.None);

        Assert.Equal(0, gitHubChannel.ScanCompletedCallCount);
        Assert.Equal(1, gitHubChannel.CriticalFindingsCallCount);
    }

    [Fact]
    public async Task DispatchForCompletedScanJobAsync_IgnoresIntegrations_FromADifferentOrganization()
    {
        await using var db = CreateInMemoryDbContext();
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var scanJob = SeedCompletedScanJob(db, organizationId, findingSeverity: null);

        var slackChannel = new FakeIntegrationChannel(IntegrationType.Slack);

        db.Integrations.Add(new Integration
        {
            OrganizationId = otherOrganizationId,
            Type = IntegrationType.Slack,
            WebhookUrl = "https://hooks.slack.test/x",
            NotifyOnScanCompletion = true
        });
        await db.SaveChangesAsync();

        var dispatcher = new NotificationDispatcher(
            db,
            new IIntegrationChannel[] { slackChannel },
            NullLogger<NotificationDispatcher>.Instance);

        await dispatcher.DispatchForCompletedScanJobAsync(scanJob.Id, CancellationToken.None);

        Assert.Equal(0, slackChannel.ScanCompletedCallCount);
    }

    [Fact]
    public async Task DispatchForCompletedScanJobAsync_DoesNothing_WhenOrganizationHasNoIntegrations()
    {
        await using var db = CreateInMemoryDbContext();
        var organizationId = Guid.NewGuid();
        var scanJob = SeedCompletedScanJob(db, organizationId, findingSeverity: null);

        var dispatcher = new NotificationDispatcher(
            db,
            Array.Empty<IIntegrationChannel>(),
            NullLogger<NotificationDispatcher>.Instance);

        // Must complete without throwing even though no channel exists to
        // resolve against.
        await dispatcher.DispatchForCompletedScanJobAsync(scanJob.Id, CancellationToken.None);
    }
}
