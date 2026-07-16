namespace StrikeShield.Application.Integrations;

public interface IIntegrationService
{
    Task<IntegrationResponse> CreateAsync(CreateIntegrationRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntegrationResponse>> GetAllAsync(Guid organizationId, CancellationToken cancellationToken = default);

    Task<IntegrationResponse> UpdateAsync(Guid id, UpdateIntegrationRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
