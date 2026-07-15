namespace StrikeShield.Application.Projects;

public interface IProjectService
{
    Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken = default);

    Task<ProjectResponse> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectResponse>> GetAllAsync(Guid? clientId, CancellationToken cancellationToken = default);

    Task<ProjectResponse> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
