using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Auth;

public interface IPasswordHasherService
{
    string HashPassword(AppUser user, string password);

    bool VerifyPassword(AppUser user, string providedPassword);
}
