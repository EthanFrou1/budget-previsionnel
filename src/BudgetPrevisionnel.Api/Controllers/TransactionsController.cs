using BudgetPrevisionnel.Api.Contracts.Transactions;
using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

/// <summary>
/// One endpoint serves both views from the brief: omit bankAccountId for the
/// consolidated view (all accounts), pass it for the per-account view. Pass
/// excludeInternalTransfers=true to avoid double-counting a transfer between two of
/// the user's own accounts as both an expense and an income - see BankImport's
/// InternalTransferMatcher for how that flag gets set at import time.
/// </summary>
[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController(TransactionService transactionService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TransactionPageResponse>> Search(
        [FromQuery] int? bankAccountId,
        [FromQuery] int? categoryId,
        [FromQuery] DateOnly? fromDate,
        [FromQuery] DateOnly? toDate,
        [FromQuery] string? search,
        [FromQuery] bool excludeInternalTransfers,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await transactionService.SearchAsync(
            currentUser.UserId, bankAccountId, categoryId, fromDate, toDate, search,
            excludeInternalTransfers, page, pageSize, cancellationToken);
        return Ok(TransactionPageResponse.FromResult(result));
    }

    [HttpPut("{id:int}/category")]
    public async Task<ActionResult<TransactionResponse>> UpdateCategory(
        int id, UpdateTransactionCategoryRequest request, CancellationToken cancellationToken)
    {
        var transaction = await transactionService.UpdateCategoryAsync(
            currentUser.UserId, id, request.CategoryId, cancellationToken);
        return Ok(TransactionResponse.FromEntity(transaction));
    }
}
