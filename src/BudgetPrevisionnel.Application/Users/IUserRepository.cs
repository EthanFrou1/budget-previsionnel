using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Users;

/// <summary>
/// Deliberately narrow (just what Auth needs today), not a generic IRepository&lt;T&gt;.
/// Extend per real need, same reasoning as the Lot 0 decision to skip a generic
/// repository/unit-of-work abstraction until something actually requires it.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
