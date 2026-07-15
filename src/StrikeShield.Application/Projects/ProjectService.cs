using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Projects;

public class ProjectService : IProjectService
{
    private readonly IAppDbContext _db;

    public ProjectService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppValidationException("Name is required.");
        }

        var clientExists = await _db.Clients.AnyAsync(c => c.Id == request.ClientId, cancellationToken);
        if (!clientExists)
        {
            throw new NotFoundException($"Client '{request.ClientId}' was not found.");
        }

        var project = new Project
        {
            ClientId = request.ClientId,
            Name = request.Name.Trim()
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        return ProjectResponse.FromEntity(project);
    }

    public async Task<ProjectResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await FindOrThrowAsync(id, cancellationToken);
        return ProjectResponse.FromEntity(project);
    }

    public async Task<IReadOnlyList<ProjectResponse>> GetAllAsync(Guid? clientId, CancellationToken cancellationToken = default)
    {
        var query = _db.Projects.AsQueryable();
        if (clientId is not null)
        {
            query = query.Where(p => p.ClientId == clientId);
        }

        var projects = await query.OrderBy(p => p.CreatedAt).ToListAsync(cancellationToken);
        return projects.Select(ProjectResponse.FromEntity).ToList();
    }

    public async Task<ProjectResponse> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppValidationException("Name is required.");
        }

        var project = await FindOrThrowAsync(id, cancellationToken);
        project.Name = request.Name.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        return ProjectResponse.FromEntity(project);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await FindOrThrowAsync(id, cancellationToken);
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Project> FindOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Project '{id}' was not found.");
    }
}
