namespace StrikeShield.Application.Findings;

public interface IFindingService
{
    Task<IReadOnlyList<FindingResponse>> GetForScanJobAsync(Guid scanJobId, CancellationToken cancellationToken = default);
}
