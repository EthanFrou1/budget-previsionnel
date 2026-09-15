namespace BudgetPrevisionnel.Application.Auth;

/// <summary>
/// The authenticated caller of the current request. Implemented in the Api layer
/// (reads JWT claims from HttpContext) so future resource services/controllers
/// (BankAccounts, Transactions...) can scope every query by UserId without
/// depending on ASP.NET Core themselves.
/// </summary>
public interface ICurrentUser
{
    int UserId { get; }
    string Email { get; }
}
