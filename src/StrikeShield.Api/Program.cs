using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StrikeShield.Api.Endpoints;
using StrikeShield.Api.Hubs;
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

// Cron-based recurring ScanJobs + the (auth-gated) dashboard mounted below
// (docs/PHASED_PLAN.md Phase 8). Same Postgres instance/connection string
// as EF Core — no new infra dependency.
var hangfireConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Missing required connection string 'ConnectionStrings:Postgres'.");

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(hangfireConnectionString)));
builder.Services.AddHangfireServer();

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
        // Browsers can't set an Authorization header on a WebSocket
        // upgrade request, so @microsoft/signalr sends the JWT as an
        // "access_token" query param instead for hub connections
        // specifically (docs/PHASED_PLAN.md Phase 9 — live progress).
        // Every other endpoint keeps using the Authorization header.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// The React SPA (docs/PHASED_PLAN.md Phase 9) is served from a different
// origin than the Api in dev (Vite on 5173) and in the Docker Compose
// stack (nginx on its own port) — see .env.example for
// STRIKESHIELD_CORS_ORIGINS. AllowCredentials is required for the
// SignalR hub connection below.
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173,http://localhost:8080")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// SignalR's hub protocol has its own JSON serializer options, entirely
// separate from ConfigureHttpJsonOptions above (which only applies to
// minimal-API/MVC responses) — without this, ScanJobProgressPayload's
// enums (ScanJobStatus, StepRunStatus) go over the wire as raw integers
// instead of the string names every REST response and the frontend's TS
// types expect, and the frontend crashes trying to look up e.g. `1`
// instead of `"Running"` in its status-badge config maps.
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddHostedService<ScanProgressBroadcastService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await DbInitializer.MigrateAndSeedAsync(app.Services);

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

// Auth-gated per docs/PHASED_PLAN.md Phase 8 — JwtAuthenticatedDashboardAuthorizationFilter
// requires an authenticated request, same as every other endpoint below.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new JwtAuthenticatedDashboardAuthorizationFilter() }
});

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
app.MapFindingsEndpoints();
app.MapScanSchedulesEndpoints();
app.MapIntegrationsEndpoints();

app.MapHub<ScanProgressHub>("/hubs/scan-progress");

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
