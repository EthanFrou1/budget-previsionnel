using BudgetPrevisionnel.Api.Contracts.Forecasting;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.Forecasting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/forecast")]
[Authorize]
public class ForecastController(ForecastService forecastService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Month accepts any day within the target month (normalized server-side to the 1st).</summary>
    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyForecastResponse>> Monthly(
        [FromQuery] DateOnly month, CancellationToken cancellationToken)
    {
        var forecast = await forecastService.GetMonthlyForecastAsync(currentUser.UserId, month, cancellationToken);
        return Ok(MonthlyForecastResponse.FromForecast(forecast));
    }

    [HttpGet("annual")]
    public async Task<ActionResult<IEnumerable<MonthlyForecastResponse>>> Annual(
        [FromQuery] int year, CancellationToken cancellationToken)
    {
        var forecasts = await forecastService.GetAnnualForecastAsync(currentUser.UserId, year, cancellationToken);
        return Ok(forecasts.Select(MonthlyForecastResponse.FromForecast));
    }
}
