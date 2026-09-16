using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Domain.Enums;

namespace BudgetPrevisionnel.Api.Contracts.RecurringIncomes;

public sealed record CreateRecurringIncomeRequest(
    string Label, decimal Amount, int? CategoryId, RecurrenceFrequency Frequency, DateOnly StartDate, DateOnly? EndDate);

public sealed record UpdateRecurringIncomeRequest(
    string Label, decimal Amount, int? CategoryId, RecurrenceFrequency Frequency, DateOnly StartDate, DateOnly? EndDate);

public sealed record RecurringIncomeResponse(
    int Id, string Label, decimal Amount, int? CategoryId, string? CategoryName,
    RecurrenceFrequency Frequency, DateOnly StartDate, DateOnly? EndDate)
{
    public static RecurringIncomeResponse FromEntity(RecurringIncome income) => new(
        income.Id, income.Label, income.Amount, income.CategoryId, income.Category?.Name,
        income.Frequency, income.StartDate, income.EndDate);
}
