using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StrikeShield.Application.Common;

namespace StrikeShield.Api.Middleware;

/// <summary>
/// Translates Application-layer exceptions into HTTP status codes, so
/// services can just throw domain-meaningful exceptions instead of every
/// endpoint handler doing its own try/catch. Notably: ForbiddenException is
/// what the Engagement scope gate throws (see ScanJobService) — it becomes
/// a 403, which is the acceptance-tested behavior for Phase 1.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (AppValidationException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (UnauthorizedAppException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database update conflict");
            await WriteProblemAsync(
                context,
                StatusCodes.Status409Conflict,
                "The request conflicts with existing data (e.g. a referenced record still has dependents).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var payload = new { status = statusCode, detail };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
