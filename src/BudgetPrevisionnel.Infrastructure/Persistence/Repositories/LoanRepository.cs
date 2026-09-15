using BudgetPrevisionnel.Application.Loans;
using BudgetPrevisionnel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Persistence.Repositories;

public sealed class LoanRepository(BudgetDbContext dbContext) : ILoanRepository
{
    public Task<Loan?> GetByIdForUserAsync(int userId, int loanId, CancellationToken cancellationToken = default) =>
        dbContext.Loans.SingleOrDefaultAsync(l => l.Id == loanId && l.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Loan>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await dbContext.Loans
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Label)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Loan loan, CancellationToken cancellationToken = default)
    {
        dbContext.Loans.Add(loan);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Loan loan, CancellationToken cancellationToken = default)
    {
        dbContext.Loans.Remove(loan);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
