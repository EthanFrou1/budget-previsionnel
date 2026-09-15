using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.SavingsGoals;

public sealed class SavingsGoalService(ISavingsGoalRepository savingsGoalRepository, IBankAccountRepository bankAccountRepository)
{
    public Task<IReadOnlyList<SavingsGoal>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        savingsGoalRepository.GetAllForUserAsync(userId, cancellationToken);

    public async Task<SavingsGoal> CreateAsync(
        int userId, string label, decimal targetAmount, decimal currentAmount, DateOnly? targetDate,
        int? linkedAccountId, CancellationToken cancellationToken = default)
    {
        ValidateAmounts(targetAmount, currentAmount);

        if (linkedAccountId is not null)
        {
            await EnsureAccountOwnedByUserAsync(userId, linkedAccountId.Value, cancellationToken);
        }

        var goal = new SavingsGoal
        {
            UserId = userId,
            Label = label.Trim(),
            TargetAmount = targetAmount,
            CurrentAmount = currentAmount,
            TargetDate = targetDate,
            LinkedAccountId = linkedAccountId
        };

        await savingsGoalRepository.AddAsync(goal, cancellationToken);
        return goal;
    }

    public async Task<SavingsGoal> UpdateAsync(
        int userId, int savingsGoalId, string label, decimal targetAmount, decimal currentAmount,
        DateOnly? targetDate, int? linkedAccountId, CancellationToken cancellationToken = default)
    {
        var goal = await savingsGoalRepository.GetByIdForUserAsync(userId, savingsGoalId, cancellationToken)
            ?? throw new SavingsGoalNotFoundException(savingsGoalId);

        ValidateAmounts(targetAmount, currentAmount);

        if (linkedAccountId is not null)
        {
            await EnsureAccountOwnedByUserAsync(userId, linkedAccountId.Value, cancellationToken);
        }

        goal.Label = label.Trim();
        goal.TargetAmount = targetAmount;
        goal.CurrentAmount = currentAmount;
        goal.TargetDate = targetDate;
        goal.LinkedAccountId = linkedAccountId;

        await savingsGoalRepository.SaveChangesAsync(cancellationToken);
        return goal;
    }

    public async Task DeleteAsync(int userId, int savingsGoalId, CancellationToken cancellationToken = default)
    {
        var goal = await savingsGoalRepository.GetByIdForUserAsync(userId, savingsGoalId, cancellationToken)
            ?? throw new SavingsGoalNotFoundException(savingsGoalId);

        await savingsGoalRepository.DeleteAsync(goal, cancellationToken);
    }

    private static void ValidateAmounts(decimal targetAmount, decimal currentAmount)
    {
        if (targetAmount <= 0)
        {
            throw new InvalidSavingsGoalException("Target amount must be greater than zero.");
        }

        if (currentAmount < 0)
        {
            throw new InvalidSavingsGoalException("Current amount cannot be negative.");
        }
    }

    private async Task EnsureAccountOwnedByUserAsync(int userId, int bankAccountId, CancellationToken cancellationToken)
    {
        var account = await bankAccountRepository.GetByIdForUserAsync(userId, bankAccountId, cancellationToken);
        if (account is null)
        {
            // LinkedAccountId is a reference inside this request, not the primary
            // resource - see InvalidReferenceException's doc comment.
            throw new InvalidReferenceException(nameof(BankAccount), bankAccountId);
        }
    }
}
