using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Application.Transactions;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.BankImport;

public sealed class BankStatementImportService(
    IEnumerable<IBankStatementParser> parsers,
    IBankAccountRepository bankAccountRepository,
    ITransactionRepository transactionRepository,
    ICategoryRepository categoryRepository,
    ICategoryRuleRepository categoryRuleRepository)
{
    public async Task<ImportSummary> ImportAsync(
        int userId, int bankAccountId, Stream fileStream, CancellationToken cancellationToken = default)
    {
        var account = await bankAccountRepository.GetByIdForUserAsync(userId, bankAccountId, cancellationToken)
            ?? throw new BankAccountNotFoundException(bankAccountId);

        var parser = parsers.FirstOrDefault(p => string.Equals(p.BankName, account.BankName, StringComparison.OrdinalIgnoreCase))
            ?? throw new NoParserAvailableException(account.BankName);

        var parsed = await parser.ParseAsync(fileStream, cancellationToken);

        var existingCounts = await transactionRepository.GetFingerprintCountsAsync(bankAccountId, cancellationToken);
        var newParsed = TransactionDeduplicator.RemoveAlreadyImported(parsed, existingCounts);

        var userRules = await categoryRuleRepository.GetByOwnerOrderedByPriorityAsync(userId, cancellationToken);

        var newTransactions = new List<Transaction>(newParsed.Count);
        var categoryIdCache = new Dictionary<string, int?>();

        foreach (var p in newParsed)
        {
            // Two-tier resolution: an exact match on the bank's own suggested category
            // wins (Lot 3); the user's CategoryRule engine (Lot 4) is only a fallback
            // for what that couldn't resolve, e.g. "Auto & Moto" has no system-category
            // equivalent but a user rule matching "TOTAL" on the label might.
            var categoryId = await ResolveSystemCategoryIdAsync(p.SuggestedCategory, categoryIdCache, cancellationToken)
                ?? CategoryRuleMatcher.Match(userRules, p.RawLabel, p.CleanedLabel);

            newTransactions.Add(new Transaction
            {
                BankAccountId = bankAccountId,
                Date = p.Date,
                RawLabel = p.RawLabel,
                CleanedLabel = p.CleanedLabel,
                Amount = p.Amount,
                CategoryId = categoryId
            });
        }

        await transactionRepository.AddRangeAsync(newTransactions, cancellationToken);

        var transferMatchCount = await DetectAndFlagInternalTransfersAsync(userId, bankAccountId, newTransactions, cancellationToken);

        return new ImportSummary(
            TotalRowsParsed: parsed.Count,
            NewTransactionsImported: newTransactions.Count,
            DuplicatesSkipped: parsed.Count - newTransactions.Count,
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
