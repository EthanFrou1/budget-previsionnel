using FluentValidation;

namespace BudgetPrevisionnel.Api.Contracts.Transactions;

// Duplicates TransactionService.UpdateNotesAsync's length check by design - see the
// note on SavingsGoalValidators.
public sealed class UpdateTransactionNotesRequestValidator : AbstractValidator<UpdateTransactionNotesRequest>
{
    public UpdateTransactionNotesRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}
