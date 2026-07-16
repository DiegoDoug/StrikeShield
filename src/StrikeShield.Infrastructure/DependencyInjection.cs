using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StrikeShield.Application.Ai;
using StrikeShield.Application.Common;
using StrikeShield.Application.Notifications;
using StrikeShield.Application.Reporting;
using StrikeShield.Application.Scheduling;
using StrikeShield.Infrastructure.Ai;
using StrikeShield.Infrastructure.HealthChecks;
using StrikeShield.Infrastructure.Notifications;
using StrikeShield.Infrastructure.Persistence;
using StrikeShield.Infrastructure.Reporting;
using StrikeShield.Infrastructure.Scheduling;

namespace StrikeShield.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Missing required connection string 'ConnectionStrings:Postgres'.");

        services.AddDbContext<StrikeShieldDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<StrikeShieldDbContext>());

        services
            .AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres");

        services.Configure<AiOrchestrationOptions>(configuration.GetSection("AiOrchestration"));

        // BYOK, same contract as Strix's LLM config (Phase 4): an empty key
        // means the Correlator's escalation pass and the Adaptive Planner
        // simply have nothing to run against, so NullLlmClient is
        // registered instead of a real HTTP-calling client — every other
        // feature keeps working without one.
        var llmApiKey = configuration["AiOrchestration:LlmApiKey"];
        if (string.IsNullOrWhiteSpace(llmApiKey))
        {
            services.AddSingleton<ILlmClient>(NullLlmClient.Instance);
        }
        else
        {
            services.AddHttpClient<ILlmClient, AnthropicLlmClient>();
        }

        services.AddScoped<IReportRenderer, PlaywrightReportRenderer>();

        // Recurring-job registration (docs/PHASED_PLAN.md Phase 8) — the
        // Hangfire server/storage/dashboard themselves are only started by
        // StrikeShield.Api's Program.cs; this registration is inert
        // (IRecurringJobManager unresolved-but-unused) in any host that
        // never calls AddHangfire, e.g. StrikeShield.Orchestrator.
        services.AddScoped<IScanScheduleRegistrar, HangfireScanScheduleRegistrar>();

        // Notification channels (docs/PHASED_PLAN.md Phase 8) — one
        // IIntegrationChannel per IntegrationType, resolved as a set via
        // IEnumerable<IIntegrationChannel> in NotificationDispatcher, the
        // same multi-implementation pattern as IFindingAdapter.
        services.AddHttpClient<SlackIntegrationChannel>();
        services.AddHttpClient<WebhookIntegrationChannel>();
        services.AddHttpClient<GitHubIntegrationChannel>();
        services.AddScoped<IIntegrationChannel>(sp => sp.GetRequiredService<SlackIntegrationChannel>());
        services.AddScoped<IIntegrationChannel>(sp => sp.GetRequiredService<WebhookIntegrationChannel>());
        services.AddScoped<IIntegrationChannel>(sp => sp.GetRequiredService<GitHubIntegrationChannel>());

        return services;
    }
}
