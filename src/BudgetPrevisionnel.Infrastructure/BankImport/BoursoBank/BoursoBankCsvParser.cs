using System.Globalization;
using BudgetPrevisionnel.Application.BankImport;
using CsvHelper;
using CsvHelper.Configuration;

namespace BudgetPrevisionnel.Infrastructure.BankImport.BoursoBank;

/// <summary>
/// Parses BoursoBank's "export-operations-*.CSV" statement export.
///
/// Column layout verified against a real export (2026-09-14), not just the bank's
/// column naming, which is actively misleading: the header row literally repeats
/// "Solde" twice - column 6 is the transaction amount (French comma decimal, e.g.
/// "-10,99" or "1 355,42"), column 10 is the real running balance (dot decimal,
/// e.g. "1421.66", not needed by the pivot model so it's never parsed). Because of
/// that duplicate header name, this parser reads fixed column INDEXES, never names.
///
/// Also verified against the real file: dates are "yyyy-MM-dd", and "Catégorie
/// Parente" (not "Catégorie") is the field whose values line up with our system
/// categories (e.g. "Abonnements & téléphonie", "Logement" match exactly; others
/// only partially - the bank-category-name to our-Category mapping itself is Lot 4).
/// "Non catégorisé" is BoursoBank's "nothing assigned" marker, not a real category
/// name, so it's normalized to null here rather than passed through as one.
/// </summary>
public sealed class BoursoBankCsvParser : IBankStatementParser
{
    private const int DateOperationColumn = 0;
    private const int LibelleColumn = 2;
    private const int LibelleSuggereColumn = 3;
    private const int CategorieParenteColumn = 5;
    private const int MontantColumn = 6; // Header text says "Solde"; this is actually the amount.

    private const string UncategorizedLabel = "Non catégorisé";

    public string BankName => "BoursoBank";

    public async Task<IReadOnlyList<ParsedBankTransaction>> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ";",
            HasHeaderRecord = true,
            MissingFieldFound = null
        };

        using var reader = new StreamReader(fileStream, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        var transactions = new List<ParsedBankTransaction>();

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawDate = csv.GetField(DateOperationColumn);
            if (string.IsNullOrWhiteSpace(rawDate))
            {
                // Blank trailing line some exporters append after the last row.
                continue;
            }

            transactions.Add(ParseRow(csv, rawDate));
        }

        return transactions;
    }

    private static ParsedBankTransaction ParseRow(CsvReader csv, string rawDate)
    {
        var date = ParseDate(rawDate);
        var rawLabel = csv.GetField(LibelleColumn) ?? string.Empty;
        var cleanedLabel = NullIfBlank(csv.GetField(LibelleSuggereColumn));
        var amount = ParseAmount(csv.GetField(MontantColumn) ?? string.Empty);
        var suggestedCategory = NormalizeCategory(csv.GetField(CategorieParenteColumn));

        return new ParsedBankTransaction(date, rawLabel, cleanedLabel, amount, suggestedCategory);
    }

    private static DateOnly ParseDate(string raw) =>
        DateOnly.ParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static decimal ParseAmount(string raw)
    {
        var cleaned = raw
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty) // non-breaking space
            .Replace(" ", string.Empty) // narrow no-break space
            .Replace(",", ".");

        return decimal.Parse(cleaned, CultureInfo.InvariantCulture);
    }

    private static string? NormalizeCategory(string? raw)
    {
        var value = NullIfBlank(raw);
        return value == UncategorizedLabel ? null : value;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
