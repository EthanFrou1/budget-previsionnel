using BudgetPrevisionnel.Api.Contracts.Loans;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.Loans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public class LoansController(LoanService loanService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LoanResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var loans = await loanService.GetAllForUserAsync(currentUser.UserId, cancellationToken);
        return Ok(loans.Select(LoanResponse.FromEntity));
    }

    [HttpPost]
    public async Task<ActionResult<LoanResponse>> Create(CreateLoanRequest request, CancellationToken cancellationToken)
    {
        var loan = await loanService.CreateAsync(
            currentUser.UserId, request.Label, request.PrincipalAmount, request.RemainingAmount,
            request.InterestRate, request.MonthlyPayment, request.EndDate, cancellationToken);
        return Ok(LoanResponse.FromEntity(loan));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LoanResponse>> Update(int id, UpdateLoanRequest request, CancellationToken cancellationToken)
    {
        var loan = await loanService.UpdateAsync(
            currentUser.UserId, id, request.Label, request.PrincipalAmount, request.RemainingAmount,
            request.InterestRate, request.MonthlyPayment, request.EndDate, cancellationToken);
        return Ok(LoanResponse.FromEntity(loan));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await loanService.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
