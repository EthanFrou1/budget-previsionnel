using BudgetPrevisionnel.Application.Users;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(BudgetDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
