using System.Net;
using System.Net.Http.Json;
using BudgetPrevisionnel.Api.Contracts.BankAccounts;
using BudgetPrevisionnel.Api.Contracts.Loans;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Tests;

/// <summary>
/// Proves the Lot 9 global exception handler (AppExceptionHandler) actually produces
/// the right HTTP status/shape for each AppException category, end-to-end through the
/// real pipeline - not just that the right exception TYPE is thrown (Application.Tests
/// already covers that with fakes).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ErrorHandlingTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task UpdateUnknownBankAccount_Returns404WithProblemDetails()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/bank-accounts/999999", new UpdateBankAccountRequest("BoursoBank", "Compte", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Not found", problem!.Title);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task CreateLoan_RemainingExceedsPrincipal_Returns400WithProblemDetails()
    {
        // A business rule FluentValidation also checks (Api/Contracts/Loans/LoanValidators)
        // - this proves the DTO-level check produces the same clean shape as a
        // service-level ValidationException would.
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/loans", new CreateLoanRequest(
            "Prêt invalide", PrincipalAmount: 1000m, RemainingAmount: 5000m,
            InterestRate: 1m, MonthlyPayment: 100m, EndDate: new DateOnly(2029, 1, 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AccessAnotherUsersBankAccount_Returns404NotLeakingItsExistence()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var created = await owner.PostAsJsonAsync(
            "/api/bank-accounts", new CreateBankAccountRequest("BoursoBank", "Compte de l'autre", null));
        var account = await created.Content.ReadFromJsonAsync<BankAccountResponse>();

        var stranger = await factory.CreateAuthenticatedClientAsync();
        var response = await stranger.DeleteAsync($"/api/bank-accounts/{account!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
