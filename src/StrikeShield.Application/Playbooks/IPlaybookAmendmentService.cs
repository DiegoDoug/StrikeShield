namespace StrikeShield.Application.Playbooks;

public interface IPlaybookAmendmentService
{
    Task<IReadOnlyList<PlaybookAmendmentResponse>> GetForScanJobAsync(Guid scanJobId, CancellationToken cancellationToken = default);

    Task<PlaybookAmendmentResponse> ApproveAsync(Guid scanJobId, Guid amendmentId, DecideAmendmentRequest request, CancellationToken cancellationToken = default);

    Task<PlaybookAmendmentResponse> RejectAsync(Guid scanJobId, Guid amendmentId, DecideAmendmentRequest request, CancellationToken cancellationToken = default);
}
