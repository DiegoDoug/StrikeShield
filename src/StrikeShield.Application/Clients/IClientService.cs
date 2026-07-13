namespace StrikeShield.Application.Clients;

public interface IClientService
{
    Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default);

    Task<ClientResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ClientResponse> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
