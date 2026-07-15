using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Clients;

public class ClientService : IClientService
{
    private readonly IAppDbContext _db;

    public ClientService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppValidationException("Name is required.");
        }

        var organizationExists = await _db.Organizations.AnyAsync(o => o.Id == request.OrganizationId, cancellationToken);
        if (!organizationExists)
        {
            throw new NotFoundException($"Organization '{request.OrganizationId}' was not found.");
        }

        var client = new Client
        {
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim()
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(cancellationToken);

        return ClientResponse.FromEntity(client);
    }

    public async Task<ClientResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await FindOrThrowAsync(id, cancellationToken);
        return ClientResponse.FromEntity(client);
    }

    public async Task<IReadOnlyList<ClientResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var clients = await _db.Clients.OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);
        return clients.Select(ClientResponse.FromEntity).ToList();
    }

    public async Task<ClientResponse> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppValidationException("Name is required.");
        }

        var client = await FindOrThrowAsync(id, cancellationToken);
        client.Name = request.Name.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        return ClientResponse.FromEntity(client);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await FindOrThrowAsync(id, cancellationToken);
        _db.Clients.Remove(client);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Client> FindOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Client '{id}' was not found.");
    }
}
