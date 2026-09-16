using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Application.Transactions;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.BankImport;

/// <summary>
/// Split into a preview/commit pair rather than the one-shot import Lot 3 originally
/// shipped: nothing is persisted until the user has reviewed the parsed rows (excluded
/// some, adjusted a suggested category) on the frontend. PreviewAsync does the parsing,
/// deduplication and auto-categorization and stops there; CommitAsync takes back exactly
/// the rows the client wants kept and persists those.
/// </summary>
public sealed class BankStatementImportService(
    IEnumerable<IBankStatementParser> parsers,
    IBankAccountRepository bankAccountRepository,
    ITransactionRepository transactionRepository,
    ICategoryRepository categoryRepository,
    ICategoryRuleRepository categoryRuleRepository)
{
    public async Task<IReadOnlyList<ImportRow>> PreviewAsync(
        int userId, int bankAccountId, Stream fileStream, CancellationToken cancellationToken = default)
    {
        var account = await bankAccountRepository.GetByIdForUserAsync(userId, bankAccountId, cancellationToken)
            ?? throw new BankAccountNotFoundException(bankAccountId);

        var parser = parsers.FirstOrDefault(p => string.Equals(p.BankName, account.BankName, StringComparison.OrdinalIgnoreCase))
            ?? throw new NoParserAvailableException(account.BankName);

        var parsed = await parser.ParseAsync(fileStream, cancellationToken);

        var existingCounts = await transactionRepository.GetFingerprintCountsAsync(bankAccountId, cancellationToken);
        var newParsed = TransactionDeduplicator.RemoveAlreadyImported(
            parsed, existingCounts, p => new TransactionFingerprint(p.Date, p.RawLabel, p.Amount));

        var userRules = await categoryRuleRepository.GetByOwnerOrderedByPriorityAsync(userId, cancellationToken);

        var rows = new List<ImportRow>(newParsed.Count);
        var categoryIdCache = new Dictionary<string, int?>();
        var categoryNameCache = new Dictionary<int, string?>();

        foreach (var p in newParsed)
        {
            // Two-tier resolution: an exact match on the bank's own suggested category
            // wins (Lot 3); the user's CategoryRule engine (Lot 4) is only a fallback
            // for what that couldn't resolve, e.g. "Auto & Moto" has no system-category
            // equivalent but a user rule matching "TOTAL" on the label might.
            var categoryId = await ResolveSystemCategoryIdAsync(p.SuggestedCategory, categoryIdCache, cancellationToken)
                ?? CategoryRuleMatcher.Match(userRules, p.RawLabel, p.CleanedLabel);
            var categoryName = categoryId is null
                ? null
                : await ResolveCategoryNameAsync(categoryId.Value, categoryNameCache, cancellationToken);

            rows.Add(new ImportRow(p.Date, p.RawLabel, p.CleanedLabel, p.Amount, categoryId, categoryName));
        }

        return rows;
    }

    public async Task<ImportSummary> CommitAsync(
        int userId, int bankAccountId, IReadOnlyList<ImportRow> rows, CancellationToken cancellationToken = default)
    {
        _ = await bankAccountRepository.GetByIdForUserAsync(userId, bankAccountId, cancellationToken)
            ?? throw new BankAccountNotFoundException(bankAccountId);

        // Re-checked here too, not just at preview: something else (another import, a
        // manual entry) could have landed a matching transaction in the time it took the
        // user to review the preview.
        var existingCounts = await transactionRepository.GetFingerprintCountsAsync(bankAccountId, cancellationToken);
        var newRows = TransactionDeduplicator.RemoveAlreadyImported(
            rows, existingCounts, r => new TransactionFingerprint(r.Date, r.RawLabel, r.Amount));

        var newTransactions = newRows.Select(r => new Transaction
        {
            BankAccountId = bankAccountId,
            Date = r.Date,
            RawLabel = r.RawLabel,
            CleanedLabel = r.CleanedLabel,
            Amount = r.Amount,
            CategoryId = r.CategoryId
        }).ToList();

        await transactionRepository.AddRangeAsync(newTransactions, cancellationToken);

        var transferMatchCount = await DetectAndFlagInternalTransfersAsync(userId, bankAccountId, newTransactions, cancellationToken);

        return new ImportSummary(
            TotalRowsParsed: rows.Count,
            NewTransactionsImported: newTransactions.Count,
            DuplicatesSkipped: rows.Count - newTransactions.Count,
            InternalTransfersDetected: transferMatchCount);
    }

    private async Task<int?> ResolveSystemCategoryIdAsync(
        string? suggestedCategory, Dictionary<string, int?> cache, CancellationToken cancellationToken)
    {
        if (suggestedCategory is null)
        {
            return null;
        }

        // Category names repeat heavily within one statement - cache within this import
        // call instead of re-querying the same name for every matching row.
        if (cache.TryGetValue(suggestedCategory, out var cached))
        {
            return cached;
        }

        var category = await categoryRepository.FindSystemCategoryByNameAsync(suggestedCategory, cancellationToken);
        cache[suggestedCategory] = category?.Id;
        return category?.Id;
    }

    private async Task<string?> ResolveCategoryNameAsync(
        int categoryId, Dictionary<int, string?> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(categoryId, out var cached))
        {
            return cached;
        }

        var category = await categoryRepository.GetByIdAsync(categoryId, cancellationToken);
        cache[categoryId] = category?.Name;
        return category?.Name;
    }

    private async Task<int> DetectAndFlagInternalTransfersAsync(
        int userId, int bankAccountId, List<Transaction> newTransactions, CancellationToken cancellationToken)
    {
        if (newTransactions.Count == 0)
        {
            return 0;
        }

        var fromDate = newTransactions.Min(t => t.Date).AddDays(-InternalTransferMatcher.MaxDateDifferenceInDays);
        var toDate = newTransactions.Max(t => t.Date).AddDays(InternalTransferMatcher.MaxDateDifferenceInDays);

        var otherAccountsTransactions = await transactionRepository.GetForOtherAccountsAsync(
            userId, bankAccountId, fromDate, toDate, cancellationToken);

        var matches = InternalTransferMatcher.FindMatches(newTransactions, otherAccountsTransactions);

        foreach (var (newTransaction, match) in matches)
        {
            newTransaction.IsInternalTransfer = true;
            match.IsInternalTransfer = true;
        }

        if (matches.Count > 0)
        {
            await transactionRepository.SaveChangesAsync(cancellationToken);
        }

        return matches.Count;
    }
}

public sealed record ImportSummary(
    int TotalRowsParsed, int NewTransactionsImported, int DuplicatesSkipped, int InternalTransfersDetected);

// Consolidated to the same 400 as every other "your input is invalid" case (was 422
// before Lot 9's exception hierarchy) - not worth a distinct status code for this app.
public sealed class NoParserAvailableException(string bankName)
    : ValidationException($"No statement parser is registered for bank '{bankName}'.");
