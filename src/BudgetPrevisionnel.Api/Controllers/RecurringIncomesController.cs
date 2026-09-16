using BudgetPrevisionnel.Api.Contracts.RecurringIncomes;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.RecurringIncomes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/recurring-incomes")]
[Authorize]
public class RecurringIncomesController(
    RecurringIncomeService recurringIncomeService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RecurringIncomeResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var incomes = await recurringIncomeService.GetAllForUserAsync(currentUser.UserId, cancellationToken);
        return Ok(incomes.Select(RecurringIncomeResponse.FromEntity));
    }

    [HttpPost]
    public async Task<ActionResult<RecurringIncomeResponse>> Create(
        CreateRecurringIncomeRequest request, CancellationToken cancellationToken)
    {
        var income = await recurringIncomeService.CreateAsync(
            currentUser.UserId, request.Label, request.Amount, request.CategoryId, request.Frequency,
            request.StartDate, request.EndDate, cancellationToken);
        return Ok(RecurringIncomeResponse.FromEntity(income));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RecurringIncomeResponse>> Update(
        int id, UpdateRecurringIncomeRequest request, CancellationToken cancellationToken)
    {
        var income = await recurringIncomeService.UpdateAsync(
            currentUser.UserId, id, request.Label, request.Amount, request.CategoryId, request.Frequency,
            request.StartDate, request.EndDate, cancellationToken);
        return Ok(RecurringIncomeResponse.FromEntity(income));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await recurringIncomeService.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
