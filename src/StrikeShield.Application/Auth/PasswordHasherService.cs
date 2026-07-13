using Microsoft.AspNetCore.Identity;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Application.Auth;

public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<AppUser> _hasher = new();

    public string HashPassword(AppUser user, string password) => _hasher.HashPassword(user, password);

    public bool VerifyPassword(AppUser user, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
