using System.Text;
using BudgetPrevisionnel.Infrastructure.BankImport.BoursoBank;

namespace BudgetPrevisionnel.Infrastructure.Tests.BankImport;

/// <summary>
/// Fixture content is synthetic (fake merchants/amounts), not the real export used to
/// validate the parser during development - real bank statements contain personal data
/// (account numbers, transfer counterparties) that has no business being committed to
/// a repo. It reproduces every real-file quirk that matters: the header's duplicate
/// "Solde" column, UTF-8 BOM, ';' delimiter, French comma decimals, a space thousands
/// separator, quoted fields, and "Non catégorisé" as BoursoBank's "no category" marker.
/// </summary>
public class BoursoBankCsvParserTests
{
    private const string Header =
        "\"Date Opération\";\"Date Valeur\";Libellé;\"Libellé Suggéré\";Catégorie;\"Catégorie Parente\";Solde;Commentaire;\"Numéro Compte\";\"Libellé Compte\";Solde;Pointage";

    [Fact]
    public async Task ParseAsync_TypicalRow_MapsAllFields()
    {
        var csv = Header + "\r\n" +
            "2026-08-31;2026-08-31;\"CARTE 29/08/26 EXAMPLE SHOP CB*1234\";\"Example Shop\";" +
            "\"Multimedia à domicile (TV, internet, téléphonie…)\";\"Abonnements & téléphonie\";" +
            "-10,99;;00000000000;BoursoBank;1000.00;Non\r\n";

        var result = await Parse(csv);

        var transaction = Assert.Single(result);
        Assert.Equal(new DateOnly(2026, 8, 31), transaction.Date);
        Assert.Equal("CARTE 29/08/26 EXAMPLE SHOP CB*1234", transaction.RawLabel);
        Assert.Equal("Example Shop", transaction.CleanedLabel);
        Assert.Equal(-10.99m, transaction.Amount);
        Assert.Equal("Abonnements & téléphonie", transaction.SuggestedCategory);
    }

    [Fact]
    public async Task ParseAsync_AmountWithThousandsSeparator_ParsesCorrectly()
    {
        var csv = Header + "\r\n" +
            "2026-08-31;2026-08-31;\"VIR SEPA EMPLOYEUR\";\"Vir Sepa Employeur\";\"Salaire fixe\";" +
            "\"Revenus du travail\";\"1 355,42\";;00000000000;BoursoBank;1000.00;Non\r\n";

        var result = await Parse(csv);

        Assert.Equal(1355.42m, Assert.Single(result).Amount);
    }

    [Fact]
    public async Task ParseAsync_UncategorizedTransaction_SuggestedCategoryIsNull()
    {
        var csv = Header + "\r\n" +
            "2026-08-30;2026-08-30;\"CARTE 29/08/26 UNKNOWN MERCHANT CB*1234\";\"Unknown Merchant\";" +
            "\"Non catégorisé\";\"Non catégorisé\";-25,00;;00000000000;BoursoBank;1000.00;Non\r\n";

        var result = await Parse(csv);

        Assert.Null(Assert.Single(result).SuggestedCategory);
    }

    [Fact]
    public async Task ParseAsync_MultipleRows_PreservesOrderAndCount()
    {
        var csv = Header + "\r\n" +
            "2026-08-05;2026-08-05;\"PRLV SEPA EXAMPLE\";Example;\"Loyers, Charges\";Logement;-10,00;;00000000000;BoursoBank;1000.00;Non\r\n" +
            "2026-08-20;2026-08-19;\"VIR INST M EXAMPLE PERSON\";\"Vir Inst M Example Person\";\"Virements reçus\";\"Virements reçus\";50,00;;00000000000;BoursoBank;1000.00;Non\r\n";

        var result = await Parse(csv);

        Assert.Equal(2, result.Count);
        Assert.Equal("Logement", result[0].SuggestedCategory);
        Assert.Equal(50.00m, result[1].Amount);
    }

    [Fact]
    public async Task ParseAsync_BlankTrailingLine_IsIgnored()
    {
        var csv = Header + "\r\n" +
            "2026-08-05;2026-08-05;\"PRLV SEPA EXAMPLE\";Example;\"Loyers, Charges\";Logement;-10,00;;00000000000;BoursoBank;1000.00;Non\r\n" +
            "\r\n";

        var result = await Parse(csv);

        Assert.Single(result);
    }

    private static async Task<IReadOnlyList<Application.BankImport.ParsedBankTransaction>> Parse(string csvContent)
    {
        var parser = new BoursoBankCsvParser();
        using var stream = ToUtf8StreamWithBom(csvContent);

        return await parser.ParseAsync(stream);
    }

    private static Stream ToUtf8StreamWithBom(string content)
    {
        var preamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble();
        var bytes = Encoding.UTF8.GetBytes(content);

        var withBom = new byte[preamble.Length + bytes.Length];
        preamble.CopyTo(withBom, 0);
        bytes.CopyTo(withBom, preamble.Length);

        return new MemoryStream(withBom);
    }
}
