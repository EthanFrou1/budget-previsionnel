using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Loans;

public interface ILoanRepository
{
    Task<Loan?> GetByIdForUserAsync(int userId, int loanId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Loan>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default);

    Task AddAsync(Loan loan, CancellationToken cancellationToken = default);

    Task DeleteAsync(Loan loan, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
