using BudgetPrevisionnel.Api.Contracts.Dashboard;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

/// <summary>
/// Every endpoint here follows the same bankAccountId convention as /api/transactions:
/// omit it for the consolidated view (all of the user's accounts), pass it for the
/// per-account view.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(DashboardService dashboardService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// CumulativeBalance is a running net-flow total, not the bank's real balance (this
    /// app doesn't persist that - see the README). Pass startingBalance if you have a
    /// known reference point; omitted, it defaults to 0 and the curve is relative.
    /// </summary>
    [HttpGet("balance-evolution")]
    public async Task<ActionResult<IEnumerable<BalancePointResponse>>> BalanceEvolution(
        [FromQuery] int? bankAccountId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        [FromQuery] decimal startingBalance, CancellationToken cancellationToken)
    {
        if (fromDate is null || toDate is null)
        {
            return BadRequest(new ProblemDetails { Title = "fromDate and toDate are required", Status = StatusCodes.Status400BadRequest });
        }

        var points = await dashboardService.GetBalanceEvolutionAsync(
            currentUser.UserId, bankAccountId, fromDate.Value, toDate.Value, startingBalance, cancellationToken);
        return Ok(points.Select(BalancePointResponse.FromModel));
    }

    /// <summary>Expense-only breakdown by category for the period - a "where did my money go" view.</summary>
    [HttpGet("category-breakdown")]
    public async Task<ActionResult<IEnumerable<CategoryBreakdownEntryResponse>>> CategoryBreakdown(
        [FromQuery] int? bankAccountId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        if (fromDate is null || toDate is null)
        {
            return BadRequest(new ProblemDetails { Title = "fromDate and toDate are required", Status = StatusCodes.Status400BadRequest });
        }

        var entries = await dashboardService.GetCategoryBreakdownAsync(
            currentUser.UserId, bankAccountId, fromDate.Value, toDate.Value, cancellationToken);
        return Ok(entries.Select(CategoryBreakdownEntryResponse.FromModel));
    }

    /// <summary>Always 12 entries (year defaults to the current year), zero-filled for
    /// months with no transactions.</summary>
    [HttpGet("monthly-comparison")]
    public async Task<ActionResult<IEnumerable<MonthlyComparisonEntryResponse>>> MonthlyComparison(
        [FromQuery] int? bankAccountId, [FromQuery] int? year, CancellationToken cancellationToken)
    {
        var entries = await dashboardService.GetMonthlyComparisonAsync(
            currentUser.UserId, bankAccountId, year ?? DateTime.UtcNow.Year, cancellationToken);
        return Ok(entries.Select(MonthlyComparisonEntryResponse.FromModel));
    }
}
