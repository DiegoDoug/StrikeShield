using Microsoft.Extensions.DependencyInjection;
using StrikeShield.Application.Auth;
using StrikeShield.Application.Clients;
using StrikeShield.Application.Engagements;
using StrikeShield.Application.Findings;
using StrikeShield.Application.Findings.Adapters;
using StrikeShield.Application.Organizations;
using StrikeShield.Application.Playbooks;
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
        services.AddScoped<IPlaybookService, PlaybookService>();
        services.AddScoped<IScanJobService, ScanJobService>();
        services.AddScoped<IAdaptivePlanner, AdaptivePlanner>();
        services.AddScoped<IPlaybookAmendmentService, PlaybookAmendmentService>();

        services.AddScoped<IFindingAdapter, NucleiFindingAdapter>();
        services.AddScoped<IFindingAdapter, SarifFindingAdapter>();
        services.AddScoped<IFindingAdapter, ZapFindingAdapter>();
        services.AddScoped<IFindingAdapter, NmapFindingAdapter>();
        services.AddScoped<IFindingAdapter, SubdomainReconFindingAdapter>();
        services.AddScoped<IFindingAdapter, KatanaFindingAdapter>();
        services.AddScoped<IFindingAdapter, FfufFindingAdapter>();
        services.AddScoped<IFindingAdapter, NiktoFindingAdapter>();
        services.AddScoped<IFindingIngestionService, FindingIngestionService>();
        services.AddScoped<ICorrelator, Correlator>();
        services.AddScoped<IFindingService, FindingService>();

        return services;
    }
}
