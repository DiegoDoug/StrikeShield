namespace StrikeShield.Application.Auth;

public record LoginRequest(string Email, string Password);

public record AuthToken(string Token, DateTime ExpiresAtUtc);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, Guid UserId, string Email, string Role);
