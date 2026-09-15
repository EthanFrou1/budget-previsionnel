using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace BudgetPrevisionnel.Infrastructure.Auth;

/// <summary>
/// Wraps ASP.NET Core Identity's PasswordHasher (PBKDF2, per-password salt) instead of
/// pulling in the rest of Identity (UserManager, SignInManager, role stores...) which
/// this project doesn't need - see AuthService for the deliberately lightweight auth flow.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _identityHasher = new();

    public string Hash(string password) => _identityHasher.HashPassword(default!, password);

    public bool Verify(string hashedPassword, string providedPassword)
    {
        var result = _identityHasher.VerifyHashedPassword(default!, hashedPassword, providedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
