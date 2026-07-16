using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace StrikeShield.Api.Hubs;

/// <summary>
/// Live scan-job progress (docs/PHASED_PLAN.md Phase 9 — "live progress via
/// SignalR"). Clients join a per-ScanJob group after connecting; the
/// hub itself never queries data — <see cref="ScanProgressBroadcastService"/>
/// is the only thing that pushes to it, polling the same Postgres database
/// the Orchestrator writes StepRun/ScanJob updates to.
/// </summary>
[Authorize]
public class ScanProgressHub : Hub
{
    public static string GroupName(Guid scanJobId) => $"scanjob-{scanJobId}";

    public Task JoinScanJob(Guid scanJobId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(scanJobId));

    public Task LeaveScanJob(Guid scanJobId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(scanJobId));
}
