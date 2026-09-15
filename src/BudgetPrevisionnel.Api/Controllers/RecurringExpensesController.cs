using BudgetPrevisionnel.Api.Contracts.RecurringExpenses;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.RecurringExpenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/recurring-expenses")]
[Authorize]
public class RecurringExpensesController(
    RecurringExpenseService recurringExpenseService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RecurringExpenseResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var expenses = await recurringExpenseService.GetAllForUserAsync(currentUser.UserId, cancellationToken);
        return Ok(expenses.Select(RecurringExpenseResponse.FromEntity));
    }

    [HttpPost]
    public async Task<ActionResult<RecurringExpenseResponse>> Create(
        CreateRecurringExpenseRequest request, CancellationToken cancellationToken)
    {
        var expense = await recurringExpenseService.CreateAsync(
            currentUser.UserId, request.Label, request.Amount, request.CategoryId, request.Frequency,
            request.StartDate, request.EndDate, cancellationToken);
        return Ok(RecurringExpenseResponse.FromEntity(expense));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecurringExpenseResponse>> Update(
        int id, UpdateRecurringExpenseRequest request, CancellationToken cancellationToken)
    {
        var expense = await recurringExpenseService.UpdateAsync(
            currentUser.UserId, id, request.Label, request.Amount, request.CategoryId, request.Frequency,
            request.StartDate, request.EndDate, cancellationToken);
        return Ok(RecurringExpenseResponse.FromEntity(expense));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await recurringExpenseService.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
