using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Api.Contracts.Loans;

public sealed record CreateLoanRequest(
    string Label, decimal PrincipalAmount, decimal RemainingAmount, decimal InterestRate,
    decimal MonthlyPayment, DateOnly EndDate);

public sealed record UpdateLoanRequest(
    string Label, decimal PrincipalAmount, decimal RemainingAmount, decimal InterestRate,
    decimal MonthlyPayment, DateOnly EndDate);

public sealed record LoanResponse(
    int Id, string Label, decimal PrincipalAmount, decimal RemainingAmount, decimal InterestRate,
    decimal MonthlyPayment, DateOnly EndDate)
{
    public static LoanResponse FromEntity(Loan loan) => new(
        loan.Id, loan.Label, loan.PrincipalAmount, loan.RemainingAmount, loan.InterestRate,
        loan.MonthlyPayment, loan.EndDate);
}
