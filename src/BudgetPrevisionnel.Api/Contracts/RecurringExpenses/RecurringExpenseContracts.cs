using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Api.Contracts.RecurringExpenses;

public sealed record CreateRecurringExpenseRequest(
    string Label, decimal Amount, int? CategoryId, RecurrenceFrequency Frequency, DateOnly StartDate, DateOnly? EndDate);

public sealed record UpdateRecurringExpenseRequest(
    string Label, decimal Amount, int? CategoryId, RecurrenceFrequency Frequency, DateOnly StartDate, DateOnly? EndDate);

public sealed record RecurringExpenseResponse(
    int Id, string Label, decimal Amount, int? CategoryId, string? CategoryName,
    RecurrenceFrequency Frequency, DateOnly StartDate, DateOnly? EndDate)
{
    public static RecurringExpenseResponse FromEntity(RecurringExpense expense) => new(
        expense.Id, expense.Label, expense.Amount, expense.CategoryId, expense.Category?.Name,
        expense.Frequency, expense.StartDate, expense.EndDate);
}
