namespace BudgetPrevisionnel.Application.BankImport;

/// <summary>
/// One row of a not-yet-persisted import - the shared shape for both directions of the
/// preview/commit round trip. PreviewAsync returns these with an auto-detected CategoryId/
/// CategoryName (or null if nothing matched); the client then resubmits exactly the rows
/// it wants persisted (some possibly excluded, others with an edited CategoryId) to
/// CommitAsync, which ignores CategoryName - only CategoryId is ever persisted.
/// </summary>
public sealed record ImportRow(
    DateOnly Date, string RawLabel, string? CleanedLabel, decimal Amount, int? CategoryId, string? CategoryName);
