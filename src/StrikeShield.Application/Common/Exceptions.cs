namespace StrikeShield.Application.Common;

/// <summary>Maps to HTTP 404 in StrikeShield.Api's exception middleware.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>Maps to HTTP 400. Thrown for malformed/inconsistent request data.</summary>
public class AppValidationException : Exception
{
    public AppValidationException(string message) : base(message)
    {
    }
}

/// <summary>
/// Maps to HTTP 403. The scope/authorization gate (Engagement not approved
/// or outside its scope window) throws this — see docs/ARCHITECTURE.md §4/§8.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}

/// <summary>Maps to HTTP 401. Thrown on failed login.</summary>
public class UnauthorizedAppException : Exception
{
    public UnauthorizedAppException(string message) : base(message)
    {
    }
}
