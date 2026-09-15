using FluentValidation;

namespace BudgetPrevisionnel.Api.Contracts.SavingsGoals;

// Duplicates SavingsGoalService.ValidateAmounts by design - a fast, well-formatted 400
// at the boundary, with the service remaining the authoritative check for any caller
// that isn't going through this API (see the Lot 9 note in docs/roadmap.md).
public sealed class CreateSavingsGoalRequestValidator : AbstractValidator<CreateSavingsGoalRequest>
{
    public CreateSavingsGoalRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.TargetAmount).GreaterThan(0);
        RuleFor(x => x.CurrentAmount).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateSavingsGoalRequestValidator : AbstractValidator<UpdateSavingsGoalRequest>
{
    public UpdateSavingsGoalRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.TargetAmount).GreaterThan(0);
        RuleFor(x => x.CurrentAmount).GreaterThanOrEqualTo(0);
    }
}
