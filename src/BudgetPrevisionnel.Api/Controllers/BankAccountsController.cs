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
        var fileContent = request.FileContentBase64 is null ? null : Convert.FromBase64String(request.FileContentBase64);
        var summary = await importService.CommitAsync(currentUser.UserId, id, request.FileName, rows, fileContent, cancellationToken);
        return Ok(ImportSummaryResponse.FromResult(summary));
    }

    /// <summary>Newest-first history of CSV imports for this account - so re-importing a
    /// statement isn't done blind to what's already been brought in before.</summary>
    [HttpGet("{id:int}/import/history")]
    public async Task<ActionResult<IEnumerable<ImportBatchResponse>>> ImportHistory(
        int id, CancellationToken cancellationToken)
    {
        var batches = await importService.GetHistoryAsync(currentUser.UserId, id, cancellationToken);
        return Ok(batches.Select(ImportBatchResponse.FromEntity));
    }

    /// <summary>Re-downloads exactly the CSV that was uploaded for one past import - 404
    /// (via ImportBatchNotFoundException) if that batch predates file capture or doesn't
    /// belong to this account.</summary>
    [HttpGet("{id:int}/import/history/{batchId:int}/file")]
    public async Task<IActionResult> ImportFile(int id, int batchId, CancellationToken cancellationToken)
    {
        var (fileName, content) = await importService.GetFileAsync(currentUser.UserId, id, batchId, cancellationToken);
        return File(content, "text/csv", fileName);
    }
}
