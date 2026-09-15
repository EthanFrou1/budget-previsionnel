using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Domain.Entities;

namespace BudgetPrevisionnel.Application.Tests.Categories;

public class CategoryRuleMatcherTests
{
    private static CategoryRule Rule(string pattern, int categoryId, int priority) =>
        new() { MatchPattern = pattern, CategoryId = categoryId, Priority = priority };

    [Fact]
    public void Match_PatternInRawLabel_ReturnsCategoryId()
    {
        var rules = new[] { Rule("TOTAL", categoryId: 3, priority: 1) };

        var result = CategoryRuleMatcher.Match(rules, "CARTE 01/08/26 TOTAL 4 CB*2856", "TotalEnergies");

        Assert.Equal(3, result);
    }

    [Fact]
    public void Match_PatternOnlyInCleanedLabel_StillMatches()
    {
        var rules = new[] { Rule("Netflix", categoryId: 4, priority: 1) };

        var result = CategoryRuleMatcher.Match(rules, "CARTE NETFLIX.COM CB*1234", "Netflix");

        Assert.Equal(4, result);
    }

    [Fact]
    public void Match_CaseInsensitive()
    {
        var rules = new[] { Rule("total", categoryId: 3, priority: 1) };

        var result = CategoryRuleMatcher.Match(rules, "CARTE TOTAL 4 CB*2856", null);

        Assert.Equal(3, result);
    }

    [Fact]
    public void Match_NoRuleMatches_ReturnsNull()
    {
        var rules = new[] { Rule("Netflix", categoryId: 4, priority: 1) };

        var result = CategoryRuleMatcher.Match(rules, "CARTE TOTAL 4 CB*2856", "TotalEnergies");

        Assert.Null(result);
    }

    [Fact]
    public void Match_MultipleRulesMatch_StopsAtTheFirstOneInTheGivenOrder()
    {
        // Match() trusts the caller's ordering (the repository sorts ascending by
        // Priority - lower first) rather than re-sorting itself.
        var rules = new[]
        {
            Rule("TOTAL", categoryId: 3, priority: 1),
            Rule("CARTE", categoryId: 1, priority: 2)
        };

        var result = CategoryRuleMatcher.Match(rules, "CARTE TOTAL 4 CB*2856", null);

        Assert.Equal(3, result);
    }
}
