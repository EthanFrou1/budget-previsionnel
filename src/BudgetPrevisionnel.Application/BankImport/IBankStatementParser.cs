namespace BudgetPrevisionnel.Application.BankImport;

/// <summary>
/// One implementation per bank (e.g. BoursoBankCsvParser). Deliberately not a generic
/// multi-bank parser: statement formats are too heterogeneous to generalize safely,
/// so each bank gets a dedicated, well-tested implementation instead.
/// </summary>
public interface IBankStatementParser
{
    /// <summary>Matches BankAccount.BankName so the import pipeline can pick the right parser.</summary>
    string BankName { get; }

    Task<IReadOnlyList<ParsedBankTransaction>> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pivot model every parser normalizes to. CleanedLabel and SuggestedCategory stay
/// null for banks that don't provide them natively; only then does a fallback
/// regex/similarity normalizer (not built yet - added when a real need shows up) kick in.
/// </summary>
public sealed record ParsedBankTransaction(
    DateOnly Date,
    string RawLabel,
    string? CleanedLabel,
    decimal Amount,
    string? SuggestedCategory);
