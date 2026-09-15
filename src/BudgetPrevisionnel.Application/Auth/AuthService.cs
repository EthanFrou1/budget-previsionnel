using BudgetPrevisionnel.Application.Users;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Auth;

public sealed class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator tokenGenerator)
{
    public async Task<AuthResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = Normalize(email);

        var existing = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            throw new EmailAlreadyInUseException(normalizedEmail);
        }

        var user = new User
        {
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(password),
            CreatedAtUtc = DateTime.UtcNow
        };

        await userRepository.AddAsync(user, cancellationToken);

        return BuildResult(user);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByEmailAsync(Normalize(email), cancellationToken);

        if (user is null || !passwordHasher.Verify(user.PasswordHash, password))
        {
            // Same exception either way: don't let a slower/different response for
            // "wrong password" vs. "no such account" leak which emails are registered.
            throw new InvalidCredentialsException();
        }

        return BuildResult(user);
    }

    private AuthResult BuildResult(User user)
    {
        var token = tokenGenerator.GenerateToken(user);
        return new AuthResult(token.Value, token.ExpiresAtUtc, user.Id, user.Email);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

public sealed record AuthResult(string Token, DateTime ExpiresAtUtc, int UserId, string Email);
