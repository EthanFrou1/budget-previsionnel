using BudgetPrevisionnel.Application.Auth;

namespace BudgetPrevisionnel.Api.Contracts.Auth;

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserResponse(int Id, string Email);

public sealed record AuthResponse(string Token, DateTime ExpiresAtUtc, UserResponse User)
{
    public static AuthResponse FromResult(AuthResult result) =>
        new(result.Token, result.ExpiresAtUtc, new UserResponse(result.UserId, result.Email));
}
