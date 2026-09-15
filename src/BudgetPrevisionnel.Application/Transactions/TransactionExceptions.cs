using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.Transactions;

public sealed class TransactionNotFoundException(int transactionId)
    : NotFoundException($"Transaction {transactionId} was not found.");
