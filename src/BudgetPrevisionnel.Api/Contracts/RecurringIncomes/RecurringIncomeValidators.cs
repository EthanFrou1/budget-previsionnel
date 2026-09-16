using FluentValidation;

namespace BudgetPrevisionnel.Api.Contracts.RecurringIncomes;

// Duplicates RecurringIncomeService.Validate by design - see the note on SavingsGoalValidators.
public sealed class CreateRecurringIncomeRequestValidator : AbstractValidator<CreateRecurringIncomeRequest>
{
    public CreateRecurringIncomeRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate is not null)
            .WithMessage("End date cannot be before the start date.");
    }
}

public sealed class UpdateRecurringIncomeRequestValidator : AbstractValidator<UpdateRecurringIncomeRequest>
{
    public UpdateRecurringIncomeRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate is not null)
            .WithMessage("End date cannot be before the start date.");
    }
}
