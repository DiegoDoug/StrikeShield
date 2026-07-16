using StrikeShield.Application.Scheduling;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Api.Tests;

/// <summary>
/// Deterministic IScanScheduleRegistrar test double — no real Hangfire
/// storage involved, the same "fixture, not live infrastructure" approach
/// FakeLlmClient.cs already uses for the LLM seam.
/// </summary>
public class FakeScanScheduleRegistrar : IScanScheduleRegistrar
{
    public int RegisterCallCount { get; private set; }
    public int RemoveCallCount { get; private set; }
    public Guid? LastRegisteredScheduleId { get; private set; }
    public Guid? LastRemovedScheduleId { get; private set; }

    public void Register(ScanSchedule schedule)
    {
        RegisterCallCount++;
        LastRegisteredScheduleId = schedule.Id;
    }

    public void Remove(Guid scheduleId)
    {
        RemoveCallCount++;
        LastRemovedScheduleId = scheduleId;
    }
}
