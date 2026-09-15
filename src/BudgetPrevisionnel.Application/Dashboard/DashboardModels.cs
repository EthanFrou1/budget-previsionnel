namespace BudgetPrevisionnel.Application.Dashboard;

public sealed record DailyNetChange(DateOnly Date, decimal NetChange);

/// <summary>
/// CumulativeBalance is a running net-flow total (StartingBalance + sum of changes so
/// far), NOT the bank's real balance - the app doesn't persist that (see Lot 3/5 notes
/// on the BoursoBank export's Solde column). Pass a real StartingBalance if you have
/// one; omitted, this is a relative "net change since the start of the period" curve.
/// </summary>
public sealed record BalancePoint(DateOnly Date, decimal NetChange, decimal CumulativeBalance);

public sealed record CategoryBreakdownEntry(int? CategoryId, string? CategoryName, decimal Amount);

public sealed record MonthlyComparisonEntry(DateOnly Month, decimal Income, decimal Expense, decimal Net);
