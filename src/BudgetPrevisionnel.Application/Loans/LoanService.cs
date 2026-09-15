using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Loans;

public sealed class LoanService(ILoanRepository loanRepository)
{
    public Task<IReadOnlyList<Loan>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        loanRepository.GetAllForUserAsync(userId, cancellationToken);

    public async Task<Loan> CreateAsync(
        int userId, string label, decimal principalAmount, decimal remainingAmount, decimal interestRate,
        decimal monthlyPayment, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        Validate(principalAmount, remainingAmount, interestRate, monthlyPayment);

        var loan = new Loan
        {
            UserId = userId,
            Label = label.Trim(),
            PrincipalAmount = principalAmount,
            RemainingAmount = remainingAmount,
            InterestRate = interestRate,
            MonthlyPayment = monthlyPayment,
            EndDate = endDate
        };

        await loanRepository.AddAsync(loan, cancellationToken);
        return loan;
    }

    public async Task<Loan> UpdateAsync(
        int userId, int loanId, string label, decimal principalAmount, decimal remainingAmount,
        decimal interestRate, decimal monthlyPayment, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var loan = await loanRepository.GetByIdForUserAsync(userId, loanId, cancellationToken)
            ?? throw new LoanNotFoundException(loanId);

        Validate(principalAmount, remainingAmount, interestRate, monthlyPayment);

        loan.Label = label.Trim();
        loan.PrincipalAmount = principalAmount;
        loan.RemainingAmount = remainingAmount;
        loan.InterestRate = interestRate;
        loan.MonthlyPayment = monthlyPayment;
        loan.EndDate = endDate;

        await loanRepository.SaveChangesAsync(cancellationToken);
        return loan;
    }

    public async Task DeleteAsync(int userId, int loanId, CancellationToken cancellationToken = default)
    {
        var loan = await loanRepository.GetByIdForUserAsync(userId, loanId, cancellationToken)
            ?? throw new LoanNotFoundException(loanId);

        await loanRepository.DeleteAsync(loan, cancellationToken);
    }

    private static void Validate(decimal principalAmount, decimal remainingAmount, decimal interestRate, decimal monthlyPayment)
    {
        if (principalAmount <= 0)
        {
            throw new InvalidLoanException("Principal amount must be greater than zero.");
        }

        if (remainingAmount < 0)
        {
            throw new InvalidLoanException("Remaining amount cannot be negative.");
        }

        if (remainingAmount > principalAmount)
        {
            throw new InvalidLoanException("Remaining amount cannot exceed the principal amount.");
        }

        if (interestRate < 0)
        {
            throw new InvalidLoanException("Interest rate cannot be negative.");
        }

        if (monthlyPayment <= 0)
        {
            throw new InvalidLoanException("Monthly payment must be greater than zero.");
        }
    }
}
