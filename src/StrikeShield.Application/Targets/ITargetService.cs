namespace StrikeShield.Application.Targets;

public interface ITargetService
{
    Task<TargetResponse> CreateAsync(CreateTargetRequest request, CancellationToken cancellationToken = default);

    Task<TargetResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TargetResponse>> GetAllAsync(Guid? projectId, CancellationToken cancellationToken = default);

    Task<TargetResponse> UpdateAsync(Guid id, UpdateTargetRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
