using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.RecurringIncomes;

public sealed class RecurringIncomeService(
    IRecurringIncomeRepository recurringIncomeRepository, ICategoryRepository categoryRepository)
{
    public Task<IReadOnlyList<RecurringIncome>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        recurringIncomeRepository.GetAllForUserAsync(userId, cancellationToken);

    public async Task<RecurringIncome> CreateAsync(
        int userId, string label, decimal amount, int? categoryId, RecurrenceFrequency frequency,
        DateOnly startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
    {
        Validate(amount, startDate, endDate);

        if (categoryId is not null)
        {
            await EnsureCategoryVisibleAsync(userId, categoryId.Value, cancellationToken);
        }

        var income = new RecurringIncome
        {
            UserId = userId,
            Label = label.Trim(),
            Amount = amount,
            CategoryId = categoryId,
            Frequency = frequency,
            StartDate = startDate,
            EndDate = endDate
        };

        await recurringIncomeRepository.AddAsync(income, cancellationToken);
        return income;
    }

    public async Task<RecurringIncome> UpdateAsync(
        int userId, int recurringIncomeId, string label, decimal amount, int? categoryId,
        RecurrenceFrequency frequency, DateOnly startDate, DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        var income = await recurringIncomeRepository.GetByIdForUserAsync(userId, recurringIncomeId, cancellationToken)
            ?? throw new RecurringIncomeNotFoundException(recurringIncomeId);

        Validate(amount, startDate, endDate);

        if (categoryId is not null)
        {
            await EnsureCategoryVisibleAsync(userId, categoryId.Value, cancellationToken);
        }

        income.Label = label.Trim();
        income.Amount = amount;
        income.CategoryId = categoryId;
        income.Frequency = frequency;
        income.StartDate = startDate;
        income.EndDate = endDate;

        await recurringIncomeRepository.SaveChangesAsync(cancellationToken);
        return income;
    }

    public async Task DeleteAsync(int userId, int recurringIncomeId, CancellationToken cancellationToken = default)
    {
        var income = await recurringIncomeRepository.GetByIdForUserAsync(userId, recurringIncomeId, cancellationToken)
            ?? throw new RecurringIncomeNotFoundException(recurringIncomeId);

        await recurringIncomeRepository.DeleteAsync(income, cancellationToken);
    }

    private static void Validate(decimal amount, DateOnly startDate, DateOnly? endDate)
    {
        // Same convention as RecurringExpense.Validate: stored as a positive magnitude,
        // the entity name already encodes direction.
        if (amount <= 0)
        {
            throw new InvalidRecurringIncomeException("Amount must be greater than zero.");
        }

        if (endDate is not null && endDate < startDate)
        {
            throw new InvalidRecurringIncomeException("End date cannot be before the start date.");
        }
    }

    private async Task EnsureCategoryVisibleAsync(int userId, int categoryId, CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        if (category is null || (!category.IsSystemDefault && category.OwnerId != userId))
        {
            throw new InvalidReferenceException(nameof(Category), categoryId);
        }
    }
}
