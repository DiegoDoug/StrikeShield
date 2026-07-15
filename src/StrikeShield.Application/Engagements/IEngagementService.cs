namespace StrikeShield.Application.Engagements;

public interface IEngagementService
{
    Task<EngagementResponse> CreateAsync(CreateEngagementRequest request, CancellationToken cancellationToken = default);

    Task<EngagementResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EngagementResponse>> GetAllAsync(Guid? projectId, CancellationToken cancellationToken = default);

    Task<EngagementResponse> ApproveAsync(Guid id, ApproveEngagementRequest request, CancellationToken cancellationToken = default);
}
