using BudgetPrevisionnel.Api.Contracts.BankAccounts;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.BankImport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/bank-accounts")]
[Authorize]
public class BankAccountsController(
    BankAccountService bankAccountService,
    BankStatementImportService importService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BankAccountResponse>> Create(CreateBankAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await bankAccountService.CreateAsync(
            currentUser.UserId, request.BankName, request.Label, request.Iban, cancellationToken);
        return Ok(BankAccountResponse.FromEntity(account));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BankAccountResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var accounts = await bankAccountService.GetAllForUserAsync(currentUser.UserId, cancellationToken);
        return Ok(accounts.Select(BankAccountResponse.FromEntity));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BankAccountResponse>> Update(int id, UpdateBankAccountRequest request, CancellationToken cancellationToken)
    {
        var account = await bankAccountService.UpdateAsync(
            currentUser.UserId, id, request.BankName, request.Label, request.Iban, cancellationToken);
        return Ok(BankAccountResponse.FromEntity(account));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await bankAccountService.DeleteAsync(currentUser.UserId, id, cancellationToken);
        return NoContent();
    }

    /// <summary>Parses and auto-categorizes the statement but persists nothing - the
    /// frontend shows this list for review (exclude rows, adjust categories) before
    /// calling Commit with whatever's left.</summary>
    [HttpPost("{id:int}/import/preview")]
    public async Task<ActionResult<IEnumerable<ImportRowResponse>>> ImportPreview(
        int id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Empty file", Status = StatusCodes.Status400BadRequest });
        }

        await using var stream = file.OpenReadStream();
        var rows = await importService.PreviewAsync(currentUser.UserId, id, stream, cancellationToken);
        return Ok(rows.Select(ImportRowResponse.FromRow));
    }

    [HttpPost("{id:int}/import/commit")]
    public async Task<ActionResult<ImportSummaryResponse>> ImportCommit(
        int id, ImportCommitRequest request, CancellationToken cancellationToken)
    {
        var rows = request.Rows.Select(r => r.ToImportRow()).ToList();
        var summary = await importService.CommitAsync(currentUser.UserId, id, rows, cancellationToken);
        return Ok(ImportSummaryResponse.FromResult(summary));
    }
}
