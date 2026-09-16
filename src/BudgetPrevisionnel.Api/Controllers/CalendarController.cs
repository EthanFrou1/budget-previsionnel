using BudgetPrevisionnel.Api.Contracts.Calendar;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/calendar")]
[Authorize]
public class CalendarController(CalendarService calendarService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Month accepts any day within the target month (normalized server-side to the 1st).</summary>
    [HttpGet("monthly")]
    public async Task<ActionResult<IEnumerable<CalendarEntryResponse>>> Monthly(
        [FromQuery] DateOnly month, CancellationToken cancellationToken)
    {
        var entries = await calendarService.GetMonthlyCalendarAsync(currentUser.UserId, month, cancellationToken);
        return Ok(entries.Select(CalendarEntryResponse.FromEntry));
    }

    [HttpGet("annual")]
    public async Task<ActionResult<IEnumerable<CalendarEntryResponse>>> Annual(
        [FromQuery] int year, CancellationToken cancellationToken)
    {
        var entries = await calendarService.GetAnnualCalendarAsync(currentUser.UserId, year, cancellationToken);
        return Ok(entries.Select(CalendarEntryResponse.FromEntry));
    }
}
