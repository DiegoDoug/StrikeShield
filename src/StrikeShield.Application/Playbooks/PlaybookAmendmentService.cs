using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Enums;

namespace StrikeShield.Application.Playbooks;

/// <summary>
/// The human-in-the-loop half of the Adaptive Planner (docs/PHASED_PLAN.md
/// Phase 6): a PlaybookAmendment never takes effect on its own — an
/// operator must call ApproveAsync/RejectAsync here first. Either decision
/// un-pauses the ScanJob (AwaitingApproval -> Queued) so the Orchestrator's
/// existing poller resumes it; approving means "use my proposed args for
/// that step," rejecting means "run it unmodified."
/// </summary>
public class PlaybookAmendmentService : IPlaybookAmendmentService
{
    private readonly IAppDbContext _db;

    public PlaybookAmendmentService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PlaybookAmendmentResponse>> GetForScanJobAsync(Guid scanJobId, CancellationToken cancellationToken = default)
    {
        var scanJobExists = await _db.ScanJobs.AnyAsync(s => s.Id == scanJobId, cancellationToken);
        if (!scanJobExists)
        {
            throw new NotFoundException($"ScanJob '{scanJobId}' was not found.");
        }

        var amendments = await _db.PlaybookAmendments
            .Include(a => a.TargetPlaybookStep)
            .Where(a => a.ScanJobId == scanJobId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return amendments.Select(PlaybookAmendmentResponse.FromEntity).ToList();
    }

    public Task<PlaybookAmendmentResponse> ApproveAsync(Guid scanJobId, Guid amendmentId, DecideAmendmentRequest request, CancellationToken cancellationToken = default) =>
        DecideAsync(scanJobId, amendmentId, PlaybookAmendmentStatus.Approved, request, cancellationToken);

    public Task<PlaybookAmendmentResponse> RejectAsync(Guid scanJobId, Guid amendmentId, DecideAmendmentRequest request, CancellationToken cancellationToken = default) =>
        DecideAsync(scanJobId, amendmentId, PlaybookAmendmentStatus.Rejected, request, cancellationToken);

    private async Task<PlaybookAmendmentResponse> DecideAsync(
        Guid scanJobId,
        Guid amendmentId,
        PlaybookAmendmentStatus decision,
        DecideAmendmentRequest request,
        CancellationToken cancellationToken)
    {
        var amendment = await _db.PlaybookAmendments
            .Include(a => a.TargetPlaybookStep)
            .FirstOrDefaultAsync(a => a.Id == amendmentId && a.ScanJobId == scanJobId, cancellationToken)
            ?? throw new NotFoundException($"PlaybookAmendment '{amendmentId}' was not found for ScanJob '{scanJobId}'.");

        if (amendment.Status != PlaybookAmendmentStatus.Pending)
        {
            throw new AppValidationException($"PlaybookAmendment '{amendmentId}' has already been {amendment.Status}.");
        }

        amendment.Status = decision;
        amendment.DecidedAt = DateTimeOffset.UtcNow;
        amendment.DecidedBy = request.DecidedBy;

        var scanJob = await _db.ScanJobs.FirstOrDefaultAsync(s => s.Id == scanJobId, cancellationToken)
            ?? throw new NotFoundException($"ScanJob '{scanJobId}' was not found.");

        if (scanJob.Status == ScanJobStatus.AwaitingApproval)
        {
            scanJob.Status = ScanJobStatus.Queued;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return PlaybookAmendmentResponse.FromEntity(amendment);
    }
}
