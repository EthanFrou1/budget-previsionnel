using System.Net;
using System.Net.Http.Json;
using System.Text;
using BudgetPrevisionnel.Api.Contracts.BankAccounts;
using BudgetPrevisionnel.Api.Contracts.Transactions;

namespace BudgetPrevisionnel.Api.Tests;

/// <summary>
/// A cross-feature smoke test (account -> import -> transactions) through the real
/// pipeline with the Lot 9 middleware/validation in place, complementing the deep
/// per-feature unit tests already covering BankImport/Transactions in isolation.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class BankAccountImportFlowTests(CustomWebApplicationFactory factory)
{
    private const string SampleCsv =
        "\"Date Opération\";\"Date Valeur\";Libellé;\"Libellé Suggéré\";Catégorie;\"Catégorie Parente\";Solde;Commentaire;\"Numéro Compte\";\"Libellé Compte\";Solde;Pointage\r\n" +
        "2026-08-05;2026-08-05;\"PRLV SEPA EXAMPLE\";Example;\"Loyers, Charges\";Logement;-700,00;;00000000009;BoursoBank;1000.00;Non\r\n";

    [Fact]
    public async Task CreateAccount_ImportStatement_TransactionAppearsInSearch()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var accountResponse = await client.PostAsJsonAsync(
            "/api/bank-accounts", new CreateBankAccountRequest("BoursoBank", "Compte courant", null));
        var account = await accountResponse.Content.ReadFromJsonAsync<BankAccountResponse>();

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(SampleCsv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "statement.csv");

        var importResponse = await client.PostAsync($"/api/bank-accounts/{account!.Id}/import", content);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var summary = await importResponse.Content.ReadFromJsonAsync<ImportSummaryResponse>();
        Assert.Equal(1, summary!.NewTransactionsImported);

        var searchResponse = await client.GetAsync($"/api/transactions?bankAccountId={account.Id}");
        var page = await searchResponse.Content.ReadFromJsonAsync<TransactionPageResponse>();
        var transaction = Assert.Single(page!.Items);
        Assert.Equal(-700.00m, transaction.Amount);
        Assert.Equal("Logement", transaction.CategoryName);
    }
}
