using BudgetPrevisionnel.Application.Loans;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.Loans;

internal sealed class FakeLoanRepository : ILoanRepository
{
    private readonly List<Loan> _loans = [];
    private int _nextId = 1;

    public Task<Loan?> GetByIdForUserAsync(int userId, int loanId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_loans.SingleOrDefault(l => l.Id == loanId && l.UserId == userId));

    public Task<IReadOnlyList<Loan>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Loan>>(_loans.Where(l => l.UserId == userId).ToList());

    public Task AddAsync(Loan loan, CancellationToken cancellationToken = default)
    {
        loan.Id = _nextId++;
        _loans.Add(loan);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Loan loan, CancellationToken cancellationToken = default)
    {
        _loans.RemoveAll(l => l.Id == loan.Id);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
