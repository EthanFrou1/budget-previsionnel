using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.BankAccounts;

public sealed class BankAccountNotFoundException(int bankAccountId)
    : NotFoundException($"Bank account {bankAccountId} was not found.");
