using Hangfire.Dashboard;

namespace StrikeShield.Api.Middleware;

/// <summary>
/// Gates the Hangfire dashboard behind the same JWT bearer auth as every
/// other endpoint (docs/PHASED_PLAN.md Phase 8: "Hangfire dashboard
/// mounted (auth-gated) for visibility"). UseAuthentication() runs earlier
/// in the pipeline than UseHangfireDashboard(), so HttpContext.User is
/// already populated from a valid bearer token by the time this filter
/// runs — full RBAC (only Owner/Admin allowed) is Phase 10 scope, same as
/// every other endpoint in this codebase today.
/// </summary>
public class JwtAuthenticatedDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}
