using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Application.RecurringExpenses;

public sealed class RecurringExpenseService(
    IRecurringExpenseRepository recurringExpenseRepository, ICategoryRepository categoryRepository)
{
    public Task<IReadOnlyList<RecurringExpense>> GetAllForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        recurringExpenseRepository.GetAllForUserAsync(userId, cancellationToken);

    public async Task<RecurringExpense> CreateAsync(
        int userId, string label, decimal amount, int? categoryId, RecurrenceFrequency frequency,
        DateOnly startDate, DateOnly? endDate, CancellationToken cancellationToken = default)
    {
        Validate(amount, startDate, endDate);

        if (categoryId is not null)
        {
            await EnsureCategoryVisibleAsync(userId, categoryId.Value, cancellationToken);
        }

        var expense = new RecurringExpense
        {
            UserId = userId,
            Label = label.Trim(),
            Amount = amount,
            CategoryId = categoryId,
            Frequency = frequency,
            StartDate = startDate,
            EndDate = endDate
        };

        await recurringExpenseRepository.AddAsync(expense, cancellationToken);
        return expense;
    }

    public async Task<RecurringExpense> UpdateAsync(
        int userId, int recurringExpenseId, string label, decimal amount, int? categoryId,
        RecurrenceFrequency frequency, DateOnly startDate, DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        var expense = await recurringExpenseRepository.GetByIdForUserAsync(userId, recurringExpenseId, cancellationToken)
            ?? throw new RecurringExpenseNotFoundException(recurringExpenseId);

        Validate(amount, startDate, endDate);

        if (categoryId is not null)
        {
            await EnsureCategoryVisibleAsync(userId, categoryId.Value, cancellationToken);
        }

        expense.Label = label.Trim();
        expense.Amount = amount;
        expense.CategoryId = categoryId;
        expense.Frequency = frequency;
        expense.StartDate = startDate;
        expense.EndDate = endDate;

        await recurringExpenseRepository.SaveChangesAsync(cancellationToken);
        return expense;
    }

    public async Task DeleteAsync(int userId, int recurringExpenseId, CancellationToken cancellationToken = default)
    {
        var expense = await recurringExpenseRepository.GetByIdForUserAsync(userId, recurringExpenseId, cancellationToken)
            ?? throw new RecurringExpenseNotFoundException(recurringExpenseId);

        await recurringExpenseRepository.DeleteAsync(expense, cancellationToken);
    }

    private static void Validate(decimal amount, DateOnly startDate, DateOnly? endDate)
    {
        // Stored as a positive magnitude, like Loan.MonthlyPayment - the entity being
        // named "Expense" already encodes direction, so no sign convention to get wrong.
        if (amount <= 0)
        {
            throw new InvalidRecurringExpenseException("Amount must be greater than zero.");
        }

        if (endDate is not null && endDate < startDate)
        {
            throw new InvalidRecurringExpenseException("End date cannot be before the start date.");
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
