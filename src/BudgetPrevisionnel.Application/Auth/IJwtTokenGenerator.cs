using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Auth;

public interface IJwtTokenGenerator
{
    AuthToken GenerateToken(User user);
}

public sealed record AuthToken(string Value, DateTime ExpiresAtUtc);
