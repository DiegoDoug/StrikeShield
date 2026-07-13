namespace StrikeShield.Application.Organizations;

public interface IOrganizationService
{
    Task<IReadOnlyList<OrganizationResponse>> GetAllAsync(CancellationToken cancellationToken = default);
}
