namespace StrikeShield.Application.ScanJobs;

public interface IScanJobService
{
    Task<ScanJobResponse> CreateAsync(CreateScanJobRequest request, CancellationToken cancellationToken = default);

    Task<ScanJobResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScanJobResponse>> GetAllAsync(Guid? engagementId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StepRunResponse>> GetStepsAsync(Guid scanJobId, CancellationToken cancellationToken = default);
}
