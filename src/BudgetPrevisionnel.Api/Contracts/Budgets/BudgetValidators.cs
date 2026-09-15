using FluentValidation;

namespace BudgetPrevisionnel.Api.Contracts.Budgets;

// Duplicates BudgetService.ValidateAmount by design - see the note on SavingsGoalValidators.
public sealed class CreateBudgetRequestValidator : AbstractValidator<CreateBudgetRequest>
{
    public CreateBudgetRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.PlannedAmount).GreaterThan(0);
    }
}

public sealed class UpdateBudgetRequestValidator : AbstractValidator<UpdateBudgetRequest>
{
    public UpdateBudgetRequestValidator()
    {
        RuleFor(x => x.PlannedAmount).GreaterThan(0);
    }
}
