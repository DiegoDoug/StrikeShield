using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using StrikeShield.Infrastructure;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = WriteHealthCheckResponse
    });

    app.MapGet("/", () => Results.Redirect("/swagger"));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "StrikeShield.Api terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var payload = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description
        }),
        totalDurationMs = report.TotalDuration.TotalMilliseconds
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

// Exposed for WebApplicationFactory<Program> in StrikeShield.Api.Tests.
public partial class Program
{
}
