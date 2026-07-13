using Docker.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StrikeShield.Infrastructure;
using StrikeShield.Orchestrator;

var builder = Host.CreateApplicationBuilder(args);

// Only needs the DbContext (via AddInfrastructure), not the web-facing
// Application services (Auth/Clients/Projects/...) — this process never
// handles an HTTP request.
builder.Services.AddInfrastructure(builder.Configuration);

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
