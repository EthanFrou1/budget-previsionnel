using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.Users;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.Auth;

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = [];
    private int _nextId = 1;

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.SingleOrDefault(u => u.Email == email));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        user.Id = _nextId++;
        _users.Add(user);
        return Task.CompletedTask;
    }
}

/// <summary>Not real hashing - just enough to tell "matches" from "doesn't" in tests.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";

    public bool Verify(string hashedPassword, string providedPassword) => hashedPassword == Hash(providedPassword);
}

internal sealed class FakeJwtTokenGenerator : IJwtTokenGenerator
{
    public AuthToken GenerateToken(User user) => new($"fake-token-for-{user.Id}", DateTime.UtcNow.AddHours(1));
}
