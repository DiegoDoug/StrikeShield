using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Auth;

public interface IJwtTokenGenerator
{
    AuthToken GenerateToken(AppUser user);
}
