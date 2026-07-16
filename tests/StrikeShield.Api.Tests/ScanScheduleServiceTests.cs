using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StrikeShield.Application.Common;
using StrikeShield.Application.Scheduling;
using StrikeShield.Domain.Entities;
using StrikeShield.Domain.Enums;
using StrikeShield.Infrastructure.Persistence;
using Xunit;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Phase 8 acceptance test (docs/PHASED_PLAN.md): "a job scheduled against
/// an expired engagement demonstrably skips with a logged reason instead
/// of running." ScanScheduleService.FireAsync re-checks
/// Engagement.CheckAuthorizedForScan on every fire (not just at
/// schedule-creation time) — these tests exercise that gate directly
/// against an in-memory DbContext, the same style CorrelatorTests.cs and
/// AdaptivePlannerTests.cs already use, with a fake IScanScheduleRegistrar
/// so no real Hangfire storage is involved.
/// </summary>
public class ScanScheduleServiceTests
{
    private static StrikeShieldDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<StrikeShieldDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new StrikeShieldDbContext(options);
    }

    private static (Engagement Engagement, Target Target, Playbook Playbook) SeedScanTargets(
        StrikeShieldDbContext db,
        DateTimeOffset scopeStart,
        DateTimeOffset scopeEnd,
        bool approved)
    {
        var projectId = Guid.NewGuid();

        var engagement = new Engagement
        {
            ProjectId = projectId,
            Name = "Engagement",
            ScopeStart = scopeStart,
            ScopeEnd = scopeEnd,
            AllowedScopeRules = new List<string> { "*.example.test" }
        };
        if (approved)
        {
            engagement.Approve("qa-lead@strikeshield.local", DateTimeOffset.UtcNow);
        }

        var target = new Target { ProjectId = projectId, Type = TargetType.Url, Value = "http://example.test" };
        var playbook = new Playbook { Slug = "nuclei-quick", Name = "Nuclei Quick Scan" };

        db.Engagements.Add(engagement);
        db.Targets.Add(target);
        db.Playbooks.Add(playbook);
        db.SaveChanges();

        return (engagement, target, playbook);
    }

    private static ScanSchedule SeedSchedule(
        StrikeShieldDbContext db,
        Engagement engagement,
        Target target,
        Playbook playbook,
        bool enabled = true)
    {
        var schedule = new ScanSchedule
        {
            EngagementId = engagement.Id,
            TargetId = target.Id,
            PlaybookId = playbook.Id,
            CronExpression = "*/5 * * * *",
            CreatedBy = "you@example.com",
            Enabled = enabled
        };
        db.ScanSchedules.Add(schedule);
        db.SaveChanges();
        return schedule;
    }

    [Fact]
    public async Task FireAsync_CreatesAQueuedScanJob_WhenEngagementIsApprovedAndInWindow()
    {
        await using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var (engagement, target, playbook) = SeedScanTargets(db, now.AddDays(-1), now.AddDays(7), approved: true);
        var schedule = SeedSchedule(db, engagement, target, playbook);

        var service = new ScanScheduleService(db, new FakeScanScheduleRegistrar(), NullLogger<ScanScheduleService>.Instance);

        await service.FireAsync(schedule.Id, CancellationToken.None);

        var scanJob = await db.ScanJobs.SingleAsync();
        Assert.Equal(ScanJobStatus.Queued, scanJob.Status);
        Assert.Equal(engagement.Id, scanJob.EngagementId);
        Assert.Equal(target.Id, scanJob.TargetId);
        Assert.Equal(playbook.Id, scanJob.PlaybookId);

        var updatedSchedule = await db.ScanSchedules.SingleAsync();
        Assert.Equal(ScanScheduleFireOutcome.Triggered, updatedSchedule.LastFireOutcome);
        Assert.Equal(scanJob.Id, updatedSchedule.LastTriggeredScanJobId);
        Assert.NotNull(updatedSchedule.LastFiredAt);
        Assert.Null(updatedSchedule.LastSkipReason);
    }

    [Fact]
    public async Task FireAsync_Skips_WhenEngagementIsNotApproved()
    {
        await using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var (engagement, target, playbook) = SeedScanTargets(db, now.AddDays(-1), now.AddDays(7), approved: false);
        var schedule = SeedSchedule(db, engagement, target, playbook);

        var service = new ScanScheduleService(db, new FakeScanScheduleRegistrar(), NullLogger<ScanScheduleService>.Instance);

        await service.FireAsync(schedule.Id, CancellationToken.None);

        Assert.Empty(db.ScanJobs);

        var updatedSchedule = await db.ScanSchedules.SingleAsync();
        Assert.Equal(ScanScheduleFireOutcome.Skipped, updatedSchedule.LastFireOutcome);
        Assert.NotNull(updatedSchedule.LastSkipReason);
        Assert.NotNull(updatedSchedule.LastFiredAt);
    }

    [Fact]
    public async Task FireAsync_Skips_WhenEngagementScopeWindowHasExpired()
    {
        await using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var (engagement, target, playbook) = SeedScanTargets(db, now.AddDays(-30), now.AddDays(-1), approved: true);
        var schedule = SeedSchedule(db, engagement, target, playbook);

        var service = new ScanScheduleService(db, new FakeScanScheduleRegistrar(), NullLogger<ScanScheduleService>.Instance);

        await service.FireAsync(schedule.Id, CancellationToken.None);

        Assert.Empty(db.ScanJobs);

        var updatedSchedule = await db.ScanSchedules.SingleAsync();
        Assert.Equal(ScanScheduleFireOutcome.Skipped, updatedSchedule.LastFireOutcome);
        Assert.Contains("scope window", updatedSchedule.LastSkipReason);
    }

    [Fact]
    public async Task FireAsync_DoesNothing_WhenScheduleIsDisabled()
    {
        await using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var (engagement, target, playbook) = SeedScanTargets(db, now.AddDays(-1), now.AddDays(7), approved: true);
        var schedule = SeedSchedule(db, engagement, target, playbook, enabled: false);

        var service = new ScanScheduleService(db, new FakeScanScheduleRegistrar(), NullLogger<ScanScheduleService>.Instance);

        await service.FireAsync(schedule.Id, CancellationToken.None);

        Assert.Empty(db.ScanJobs);

        var updatedSchedule = await db.ScanSchedules.SingleAsync();
        Assert.Null(updatedSchedule.LastFiredAt);
        Assert.Null(updatedSchedule.LastFireOutcome);
    }

    [Fact]
    public async Task FireAsync_DoesNothing_WhenTheScheduleNoLongerExists()
    {
        await using var db = CreateInMemoryDbContext();

        var service = new ScanScheduleService(db, new FakeScanScheduleRegistrar(), NullLogger<ScanScheduleService>.Instance);

        await service.FireAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(db.ScanJobs);
    }

    [Fact]
    public async Task CreateAsync_RegistersWithTheScheduler_BeforePersisting()
    {
        await using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var (engagement, target, playbook) = SeedScanTargets(db, now.AddDays(-1), now.AddDays(7), approved: true);

        var registrar = new FakeScanScheduleRegistrar();
        var service = new ScanScheduleService(db, registrar, NullLogger<ScanScheduleService>.Instance);

        var response = await service.CreateAsync(
            new CreateScanScheduleRequest(engagement.Id, target.Id, playbook.Slug, "*/5 * * * *", "you@example.com"),
            CancellationToken.None);

        Assert.Equal(1, registrar.RegisterCallCount);
        Assert.Equal(response.Id, registrar.LastRegisteredScheduleId);

        var persisted = await db.ScanSchedules.SingleAsync();
        Assert.Equal("*/5 * * * *", persisted.CronExpression);
    }

    [Fact]
    public async Task CreateAsync_ThrowsAndNeverPersists_WhenCronExpressionIsMalformed()
    {
        await using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var (engagement, target, playbook) = SeedScanTargets(db, now.AddDays(-1), now.AddDays(7), approved: true);

        var registrar = new FakeScanScheduleRegistrar();
        var service = new ScanScheduleService(db, registrar, NullLogger<ScanScheduleService>.Instance);

        await Assert.ThrowsAsync<AppValidationException>(() => service.CreateAsync(
            new CreateScanScheduleRequest(engagement.Id, target.Id, playbook.Slug, "not-a-cron", "you@example.com"),
            CancellationToken.None));

        Assert.Empty(db.ScanSchedules);
        Assert.Equal(0, registrar.RegisterCallCount);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFromTheScheduler_AndPersistence()
    {
        await using var db = CreateInMemoryDbContext();
        var now = DateTimeOffset.UtcNow;
        var (engagement, target, playbook) = SeedScanTargets(db, now.AddDays(-1), now.AddDays(7), approved: true);
        var schedule = SeedSchedule(db, engagement, target, playbook);

        var registrar = new FakeScanScheduleRegistrar();
        var service = new ScanScheduleService(db, registrar, NullLogger<ScanScheduleService>.Instance);

        await service.DeleteAsync(schedule.Id, CancellationToken.None);

        Assert.Equal(1, registrar.RemoveCallCount);
        Assert.Equal(schedule.Id, registrar.LastRemovedScheduleId);
        Assert.Empty(db.ScanSchedules);
    }
}
