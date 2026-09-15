using FluentValidation;

namespace BudgetPrevisionnel.Api.Contracts.Loans;

// Duplicates LoanService.Validate by design - see the note on SavingsGoalValidators.
public sealed class CreateLoanRequestValidator : AbstractValidator<CreateLoanRequest>
{
    public CreateLoanRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.PrincipalAmount).GreaterThan(0);
        RuleFor(x => x.RemainingAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RemainingAmount).LessThanOrEqualTo(x => x.PrincipalAmount)
            .WithMessage("Remaining amount cannot exceed the principal amount.");
        RuleFor(x => x.InterestRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MonthlyPayment).GreaterThan(0);
    }
}

public sealed class UpdateLoanRequestValidator : AbstractValidator<UpdateLoanRequest>
{
    public UpdateLoanRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.PrincipalAmount).GreaterThan(0);
        RuleFor(x => x.RemainingAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RemainingAmount).LessThanOrEqualTo(x => x.PrincipalAmount)
            .WithMessage("Remaining amount cannot exceed the principal amount.");
        RuleFor(x => x.InterestRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MonthlyPayment).GreaterThan(0);
    }
}
