using FluentValidation;

namespace BudgetPrevisionnel.Api.Contracts.RecurringExpenses;

// Duplicates RecurringExpenseService.Validate by design - see the note on SavingsGoalValidators.
public sealed class CreateRecurringExpenseRequestValidator : AbstractValidator<CreateRecurringExpenseRequest>
{
    public CreateRecurringExpenseRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate is not null)
            .WithMessage("End date cannot be before the start date.");
    }
}

public sealed class UpdateRecurringExpenseRequestValidator : AbstractValidator<UpdateRecurringExpenseRequest>
{
    public UpdateRecurringExpenseRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate is not null)
            .WithMessage("End date cannot be before the start date.");
    }
}
