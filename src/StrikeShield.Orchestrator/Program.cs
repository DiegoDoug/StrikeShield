using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StrikeShield.Application;
using StrikeShield.Infrastructure;
using StrikeShield.Orchestrator;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

// Needed for the Finding adapters/ingestion/Correlator (Phase 3) that
// PlaybookExecutor calls after each step — the web-facing Auth/Clients/
// Projects/... services this also registers are simply unused here since
// this process never handles an HTTP request.
builder.Services.AddApplication();

builder.Services.Configure<OrchestratorOptions>(builder.Configuration.GetSection("Orchestrator"));

builder.Services.AddSingleton<IDockerClient>(_ =>
{
    var dockerHost = builder.Configuration["Docker:Host"] ?? "unix:///var/run/docker.sock";
    return new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
});

builder.Services.AddScoped<IPlaybookExecutor, PlaybookExecutor>();
builder.Services.AddHostedService<ScanJobRunnerService>();

var host = builder.Build();
host.Run();
