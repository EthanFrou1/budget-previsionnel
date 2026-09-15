using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Categories;

/// <summary>
/// Fallback categorization when the bank's own suggested category doesn't map to one of
/// our system categories (BankStatementImportService tries the exact-name match first).
/// Checks a transaction's raw AND cleaned label against each of the user's rules, in
/// Priority order (lower first), and stops at the first match.
/// </summary>
public static class CategoryRuleMatcher
{
    public static int? Match(IReadOnlyList<CategoryRule> rulesByPriority, string rawLabel, string? cleanedLabel)
    {
        foreach (var rule in rulesByPriority)
        {
            if (Contains(rawLabel, rule.MatchPattern) || (cleanedLabel is not null && Contains(cleanedLabel, rule.MatchPattern)))
            {
                return rule.CategoryId;
            }
        }

        return null;
    }

    private static bool Contains(string text, string pattern) =>
        text.Contains(pattern, StringComparison.OrdinalIgnoreCase);
}
