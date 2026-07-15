using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StrikeShield.Api.Endpoints;
using StrikeShield.Api.Middleware;
using StrikeShield.Application;
using StrikeShield.Infrastructure;
using StrikeShield.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Deliberately not using Serilog's two-stage bootstrap-logger pattern
// (a static Log.Logger reassigned at the top of this file): it races
// when multiple WebApplicationFactory-built hosts run concurrently in the
// same process (xUnit runs different test classes' collections in
// parallel by default), each re-executing these top-level statements —
// "System.InvalidOperationException: The logger is already frozen."
// Configuring Serilog only through UseSerilog keeps logging scoped to
// each host's own DI container instead of a shared static field.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"]
    ?? throw new InvalidOperationException("Missing required configuration 'Jwt:Key'.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await DbInitializer.MigrateAndSeedAsync(app.Services);

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthCheckResponse
});

app.MapGet("/", () => Results.Redirect("/swagger"));

app.MapAuthEndpoints();
app.MapOrganizationsEndpoints();
app.MapClientsEndpoints();
app.MapProjectsEndpoints();
app.MapTargetsEndpoints();
app.MapEngagementsEndpoints();
app.MapPlaybooksEndpoints();
app.MapScanJobsEndpoints();

app.Run();

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
