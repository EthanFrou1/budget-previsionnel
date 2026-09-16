using System.Net;
using System.Net.Http.Json;
using System.Text;
using BudgetPrevisionnel.Api.Contracts.BankAccounts;
using BudgetPrevisionnel.Api.Contracts.Transactions;

namespace BudgetPrevisionnel.Api.Tests;

/// <summary>
/// A cross-feature smoke test (account -> import preview -> commit -> transactions)
/// through the real pipeline with the Lot 9 middleware/validation in place,
/// complementing the deep per-feature unit tests already covering
/// BankImport/Transactions in isolation.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class BankAccountImportFlowTests(CustomWebApplicationFactory factory)
{
    private const string SampleCsv =
        "\"Date Opération\";\"Date Valeur\";Libellé;\"Libellé Suggéré\";Catégorie;\"Catégorie Parente\";Solde;Commentaire;\"Numéro Compte\";\"Libellé Compte\";Solde;Pointage\r\n" +
        "2026-08-05;2026-08-05;\"PRLV SEPA EXAMPLE\";Example;\"Loyers, Charges\";Logement;-700,00;;00000000009;BoursoBank;1000.00;Non\r\n";

    private static async Task<HttpResponseMessage> PreviewSampleCsvAsync(HttpClient client, int accountId)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(SampleCsv));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", "statement.csv");

        return await client.PostAsync($"/api/bank-accounts/{accountId}/import/preview", content);
    }

    [Fact]
    public async Task CreateAccount_PreviewThenCommitStatement_TransactionAppearsInSearch()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var accountResponse = await client.PostAsJsonAsync(
            "/api/bank-accounts", new CreateBankAccountRequest("BoursoBank", "Compte courant", null));
        var account = await accountResponse.Content.ReadFromJsonAsync<BankAccountResponse>();

        var previewResponse = await PreviewSampleCsvAsync(client, account!.Id);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var previewRows = await previewResponse.Content.ReadFromJsonAsync<List<ImportRowResponse>>();
        var previewRow = Assert.Single(previewRows!);
        Assert.Equal(-700.00m, previewRow.Amount);
        Assert.Equal("Logement", previewRow.CategoryName);

        var commitRequest = new ImportCommitRequest(
            "statement.csv",
            [new ImportCommitRowRequest(previewRow.Date, previewRow.RawLabel, previewRow.CleanedLabel, previewRow.Amount, previewRow.CategoryId)]);
        var commitResponse = await client.PostAsJsonAsync($"/api/bank-accounts/{account.Id}/import/commit", commitRequest);
        Assert.Equal(HttpStatusCode.OK, commitResponse.StatusCode);
        var summary = await commitResponse.Content.ReadFromJsonAsync<ImportSummaryResponse>();
        Assert.Equal(1, summary!.NewTransactionsImported);

        var searchResponse = await client.GetAsync($"/api/transactions?bankAccountId={account.Id}");
        var page = await searchResponse.Content.ReadFromJsonAsync<TransactionPageResponse>();
        var transaction = Assert.Single(page!.Items);
        Assert.Equal(-700.00m, transaction.Amount);
        Assert.Equal("Logement", transaction.CategoryName);
    }

    [Fact]
    public async Task CommittedImport_AppearsInTheHistoryEndpoint()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var accountResponse = await client.PostAsJsonAsync(
            "/api/bank-accounts", new CreateBankAccountRequest("BoursoBank", "Compte courant", null));
        var account = await accountResponse.Content.ReadFromJsonAsync<BankAccountResponse>();

        var previewResponse = await PreviewSampleCsvAsync(client, account!.Id);
        var previewRows = await previewResponse.Content.ReadFromJsonAsync<List<ImportRowResponse>>();
        var previewRow = Assert.Single(previewRows!);
        var commitRequest = new ImportCommitRequest(
            "statement.csv",
            [new ImportCommitRowRequest(previewRow.Date, previewRow.RawLabel, previewRow.CleanedLabel, previewRow.Amount, previewRow.CategoryId)]);
        await client.PostAsJsonAsync($"/api/bank-accounts/{account.Id}/import/commit", commitRequest);

        var historyResponse = await client.GetAsync($"/api/bank-accounts/{account.Id}/import/history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<List<ImportBatchResponse>>();
        var batch = Assert.Single(history!);
        Assert.Equal("statement.csv", batch.FileName);
        Assert.Equal(1, batch.NewTransactionsImported);
    }

    [Fact]
    public async Task Preview_DoesNotPersistAnything()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var accountResponse = await client.PostAsJsonAsync(
            "/api/bank-accounts", new CreateBankAccountRequest("BoursoBank", "Compte courant", null));
        var account = await accountResponse.Content.ReadFromJsonAsync<BankAccountResponse>();

        await PreviewSampleCsvAsync(client, account!.Id);

        var searchResponse = await client.GetAsync($"/api/transactions?bankAccountId={account.Id}");
        var page = await searchResponse.Content.ReadFromJsonAsync<TransactionPageResponse>();
        Assert.Empty(page!.Items);
    }

    [Fact]
    public async Task CommittedImportWithFileContent_CanBeReDownloaded()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var accountResponse = await client.PostAsJsonAsync(
            "/api/bank-accounts", new CreateBankAccountRequest("BoursoBank", "Compte courant", null));
        var account = await accountResponse.Content.ReadFromJsonAsync<BankAccountResponse>();

        var previewResponse = await PreviewSampleCsvAsync(client, account!.Id);
        var previewRows = await previewResponse.Content.ReadFromJsonAsync<List<ImportRowResponse>>();
        var previewRow = Assert.Single(previewRows!);
        var csvBytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(SampleCsv);
        var commitRequest = new ImportCommitRequest(
            "statement.csv",
            [new ImportCommitRowRequest(previewRow.Date, previewRow.RawLabel, previewRow.CleanedLabel, previewRow.Amount, previewRow.CategoryId)],
            Convert.ToBase64String(csvBytes));
        await client.PostAsJsonAsync($"/api/bank-accounts/{account.Id}/import/commit", commitRequest);

        var historyResponse = await client.GetAsync($"/api/bank-accounts/{account.Id}/import/history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<ImportBatchResponse>>();
        var batch = Assert.Single(history!);
        Assert.True(batch.HasStoredFile);

        var fileResponse = await client.GetAsync($"/api/bank-accounts/{account.Id}/import/history/{batch.Id}/file");
        Assert.Equal(HttpStatusCode.OK, fileResponse.StatusCode);
        var downloadedBytes = await fileResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(csvBytes, downloadedBytes);
    }

    [Fact]
    public async Task CommittedImportWithoutFileContent_HasNoStoredFileAndDownloadReturns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var accountResponse = await client.PostAsJsonAsync(
            "/api/bank-accounts", new CreateBankAccountRequest("BoursoBank", "Compte courant", null));
        var account = await accountResponse.Content.ReadFromJsonAsync<BankAccountResponse>();

        var previewResponse = await PreviewSampleCsvAsync(client, account!.Id);
        var previewRows = await previewResponse.Content.ReadFromJsonAsync<List<ImportRowResponse>>();
        var previewRow = Assert.Single(previewRows!);
        var commitRequest = new ImportCommitRequest(
            "statement.csv",
            [new ImportCommitRowRequest(previewRow.Date, previewRow.RawLabel, previewRow.CleanedLabel, previewRow.Amount, previewRow.CategoryId)]);
        await client.PostAsJsonAsync($"/api/bank-accounts/{account.Id}/import/commit", commitRequest);

        var historyResponse = await client.GetAsync($"/api/bank-accounts/{account.Id}/import/history");
        var history = await historyResponse.Content.ReadFromJsonAsync<List<ImportBatchResponse>>();
        var batch = Assert.Single(history!);
        Assert.False(batch.HasStoredFile);

        var fileResponse = await client.GetAsync($"/api/bank-accounts/{account.Id}/import/history/{batch.Id}/file");
        Assert.Equal(HttpStatusCode.NotFound, fileResponse.StatusCode);
    }

    [Fact]
    public async Task Commit_RowExcludedFromThePreview_IsNeverImported()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var accountResponse = await client.PostAsJsonAsync(
            "/api/bank-accounts", new CreateBankAccountRequest("BoursoBank", "Compte courant", null));
        var account = await accountResponse.Content.ReadFromJsonAsync<BankAccountResponse>();

        await PreviewSampleCsvAsync(client, account!.Id);

        // The user reviewed the preview and excluded the only row - commit sends an empty list.
        var commitRequest = new ImportCommitRequest("statement.csv", []);
        var commitResponse = await client.PostAsJsonAsync($"/api/bank-accounts/{account.Id}/import/commit", commitRequest);
        var summary = await commitResponse.Content.ReadFromJsonAsync<ImportSummaryResponse>();
        Assert.Equal(0, summary!.NewTransactionsImported);

        var searchResponse = await client.GetAsync($"/api/transactions?bankAccountId={account.Id}");
        var page = await searchResponse.Content.ReadFromJsonAsync<TransactionPageResponse>();
        Assert.Empty(page!.Items);
    }
}
