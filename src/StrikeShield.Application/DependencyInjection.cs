using Microsoft.Extensions.DependencyInjection;
using StrikeShield.Application.Auth;
using StrikeShield.Application.Clients;
using StrikeShield.Application.Engagements;
using StrikeShield.Application.Organizations;
using StrikeShield.Application.Projects;
using StrikeShield.Application.ScanJobs;
using StrikeShield.Application.Targets;

namespace StrikeShield.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasherService, PasswordHasherService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITargetService, TargetService>();
        services.AddScoped<IEngagementService, EngagementService>();
        services.AddScoped<IScanJobService, ScanJobService>();

        return services;
    }
}
