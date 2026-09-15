using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Application.Transactions;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Budgets;

/// <summary>Budget.PlannedAmount paired with the actual computed for the same category/month.</summary>
public sealed record BudgetLine(Budget Budget, decimal ActualAmount);

public sealed class BudgetService(
    IBudgetRepository budgetRepository, ICategoryRepository categoryRepository, ITransactionRepository transactionRepository)
{
    public async Task<IReadOnlyList<BudgetLine>> GetForMonthAsync(
        int userId, DateOnly month, CancellationToken cancellationToken = default)
    {
        var normalizedMonth = Normalize(month);
        var budgets = await budgetRepository.GetForMonthAsync(userId, normalizedMonth, cancellationToken);

        var lines = new List<BudgetLine>(budgets.Count);
        foreach (var budget in budgets)
        {
            var actual = await ComputeActualAsync(userId, budget.CategoryId, normalizedMonth, cancellationToken);
            lines.Add(new BudgetLine(budget, actual));
        }

        return lines;
    }

    public async Task<BudgetLine> CreateAsync(
        int userId, DateOnly month, int categoryId, decimal plannedAmount, CancellationToken cancellationToken = default)
    {
        var normalizedMonth = Normalize(month);
        ValidateAmount(plannedAmount);
        var category = await EnsureCategoryVisibleAsync(userId, categoryId, cancellationToken);

        var existing = await budgetRepository.FindAsync(userId, normalizedMonth, categoryId, cancellationToken);
        if (existing is not null)
        {
            throw new DuplicateBudgetException(normalizedMonth, categoryId);
        }

        var budget = new Budget
        {
            UserId = userId,
            Month = normalizedMonth,
            CategoryId = categoryId,
            Category = category,
            PlannedAmount = plannedAmount
        };

        await budgetRepository.AddAsync(budget, cancellationToken);

        var actual = await ComputeActualAsync(userId, categoryId, normalizedMonth, cancellationToken);
        return new BudgetLine(budget, actual);
    }

    public async Task<BudgetLine> UpdateAsync(
        int userId, int budgetId, decimal plannedAmount, CancellationToken cancellationToken = default)
    {
        var budget = await budgetRepository.GetByIdForUserAsync(userId, budgetId, cancellationToken)
            ?? throw new BudgetNotFoundException(budgetId);

        ValidateAmount(plannedAmount);
        budget.PlannedAmount = plannedAmount;

        await budgetRepository.SaveChangesAsync(cancellationToken);

        var actual = await ComputeActualAsync(userId, budget.CategoryId, budget.Month, cancellationToken);
        return new BudgetLine(budget, actual);
    }

    public async Task DeleteAsync(int userId, int budgetId, CancellationToken cancellationToken = default)
    {
        var budget = await budgetRepository.GetByIdForUserAsync(userId, budgetId, cancellationToken)
            ?? throw new BudgetNotFoundException(budgetId);

        await budgetRepository.DeleteAsync(budget, cancellationToken);
    }

    private async Task<decimal> ComputeActualAsync(int userId, int categoryId, DateOnly month, CancellationToken cancellationToken)
    {
        var toDate = month.AddMonths(1).AddDays(-1);
        var net = await transactionRepository.GetNetAmountAsync(userId, categoryId, month, toDate, cancellationToken);

        // Transactions in an expense category sum negative; flip to a positive "spent"
        // figure that's directly comparable to PlannedAmount's positive-magnitude convention.
        return -net;
    }

    private static DateOnly Normalize(DateOnly month) => new(month.Year, month.Month, 1);

    private static void ValidateAmount(decimal plannedAmount)
    {
        if (plannedAmount <= 0)
        {
            throw new InvalidBudgetException("Planned amount must be greater than zero.");
        }
    }

    private async Task<Category> EnsureCategoryVisibleAsync(int userId, int categoryId, CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        if (category is null || (!category.IsSystemDefault && category.OwnerId != userId))
        {
            throw new InvalidReferenceException(nameof(Category), categoryId);
        }

        return category;
    }
}
