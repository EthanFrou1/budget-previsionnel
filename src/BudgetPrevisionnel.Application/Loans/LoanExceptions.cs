using BudgetPrevisionnel.Application.Common;

namespace BudgetPrevisionnel.Application.Loans;

public sealed class LoanNotFoundException(int loanId)
    : NotFoundException($"Loan {loanId} was not found.");

public sealed class InvalidLoanException(string message) : ValidationException(message);
