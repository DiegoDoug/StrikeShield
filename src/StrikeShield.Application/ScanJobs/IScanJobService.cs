namespace StrikeShield.Application.ScanJobs;

public interface IScanJobService
{
    Task<ScanJobResponse> CreateAsync(CreateScanJobRequest request, CancellationToken cancellationToken = default);

    Task<ScanJobResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScanJobResponse>> GetAllAsync(Guid? engagementId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StepRunResponse>> GetStepsAsync(Guid scanJobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every Asset discovered by any step of this ScanJob (docs/PHASED_PLAN.md
    /// Phase 5) — subdomains/URLs/hosts/ports a recon step found, regardless
    /// of whether a downstream step went on to consume them.
    /// </summary>
    Task<IReadOnlyList<AssetResponse>> GetAssetsAsync(Guid scanJobId, CancellationToken cancellationToken = default);
}
