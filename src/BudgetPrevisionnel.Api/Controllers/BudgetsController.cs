using BudgetPrevisionnel.Api.Contracts.Budgets;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.Budgets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/budgets")]
[Authorize]
public class BudgetsController(BudgetService budgetService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Month accepts any day within the target month (normalized server-side to the 1st).</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BudgetLineResponse>>> GetForMonth(
        [FromQuery] DateOnly month, CancellationToken cancellationToken)
    {
        var lines = await budgetService.GetForMonthAsync(currentUser.UserId, month, cancellationToken);
        return Ok(lines.Select(BudgetLineResponse.FromLine));
    }

    [HttpPost]
    public async Task<ActionResult<BudgetLineResponse>> Create(CreateBudgetRequest request, CancellationToken cancellationToken)
    {
        var line = await budgetService.CreateAsync(
            currentUser.UserId, request.Month, request.CategoryId, request.PlannedAmount, cancellationToken);
        return Ok(BudgetLineResponse.FromLine(line));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BudgetLineResponse>> Update(int id, UpdateBudgetRequest request, CancellationToken cancellationToken)
    {
        var line = await budgetService.UpdateAsync(currentUser.UserId, id, request.PlannedAmount, cancellationToken);
        return Ok(BudgetLineResponse.FromLine(line));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await budgetService.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
