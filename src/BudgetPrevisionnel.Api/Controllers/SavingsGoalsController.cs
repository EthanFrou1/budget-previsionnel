using BudgetPrevisionnel.Api.Contracts.SavingsGoals;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.SavingsGoals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/savings-goals")]
[Authorize]
public class SavingsGoalsController(SavingsGoalService savingsGoalService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SavingsGoalResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var goals = await savingsGoalService.GetAllForUserAsync(currentUser.UserId, cancellationToken);
        return Ok(goals.Select(SavingsGoalResponse.FromEntity));
    }

    [HttpPost]
    public async Task<ActionResult<SavingsGoalResponse>> Create(CreateSavingsGoalRequest request, CancellationToken cancellationToken)
    {
        var goal = await savingsGoalService.CreateAsync(
            currentUser.UserId, request.Label, request.TargetAmount, request.CurrentAmount,
            request.TargetDate, request.LinkedAccountId, cancellationToken);
        return Ok(SavingsGoalResponse.FromEntity(goal));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SavingsGoalResponse>> Update(int id, UpdateSavingsGoalRequest request, CancellationToken cancellationToken)
    {
        var goal = await savingsGoalService.UpdateAsync(
            currentUser.UserId, id, request.Label, request.TargetAmount, request.CurrentAmount,
            request.TargetDate, request.LinkedAccountId, cancellationToken);
        return Ok(SavingsGoalResponse.FromEntity(goal));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await savingsGoalService.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
