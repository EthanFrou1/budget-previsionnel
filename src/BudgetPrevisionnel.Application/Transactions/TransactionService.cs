using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Application.Common;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Transactions;

public sealed class TransactionService(
    ITransactionRepository transactionRepository, IBankAccountRepository bankAccountRepository, ICategoryRepository categoryRepository)
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public async Task<TransactionPage> SearchAsync(
        int userId, int? bankAccountId, int? categoryId, DateOnly? fromDate, DateOnly? toDate,
        string? search, bool excludeInternalTransfers, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (bankAccountId is not null)
        {
            // Scoping the whole query by UserId already prevents leaking another user's
            // transactions, but this gives a clean 404 for an account id that isn't
            // yours instead of a silently-empty page.
            var account = await bankAccountRepository.GetByIdForUserAsync(userId, bankAccountId.Value, cancellationToken);
            if (account is null)
            {
                throw new BankAccountNotFoundException(bankAccountId.Value);
            }
        }

        var query = new TransactionQuery(
            UserId: userId,
            BankAccountId: bankAccountId,
            CategoryId: categoryId,
            FromDate: fromDate,
            ToDate: toDate,
            Search: string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            ExcludeInternalTransfers: excludeInternalTransfers,
            Page: Math.Max(page, 1),
            PageSize: Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize));

        return await transactionRepository.SearchAsync(query, cancellationToken);
    }

    /// <summary>Manual (re-)categorization from the transactions screen - not part of the
    /// import pipeline (BankStatementImportService/CategoryRuleMatcher), which only sets
    /// CategoryId at import time. CategoryId null means "mark as uncategorized".</summary>
    public async Task<Transaction> UpdateCategoryAsync(
        int userId, int transactionId, int? categoryId, CancellationToken cancellationToken = default)
    {
        var transaction = await transactionRepository.GetByIdForUserAsync(userId, transactionId, cancellationToken)
            ?? throw new TransactionNotFoundException(transactionId);

        Category? category = null;
        if (categoryId is not null)
        {
            category = await categoryRepository.GetByIdAsync(categoryId.Value, cancellationToken);
            if (category is null || (!category.IsSystemDefault && category.OwnerId != userId))
            {
                throw new InvalidReferenceException(nameof(Category), categoryId.Value);
            }
        }

        // Set the navigation, not just the FK: TransactionResponse.FromEntity reads
        // transaction.Category?.Name right after this returns, and relying on EF's
        // change-tracker fixup to resolve it from the FK alone is less direct than just
        // assigning what we already fetched.
        transaction.CategoryId = categoryId;
        transaction.Category = category;
        await transactionRepository.SaveChangesAsync(cancellationToken);
        return transaction;
    }
}
