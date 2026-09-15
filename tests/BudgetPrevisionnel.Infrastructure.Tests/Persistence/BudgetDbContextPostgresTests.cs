using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace BudgetPrevisionnel.Infrastructure.Tests.Persistence;

/// <summary>
/// Runs against a real, ephemeral Postgres container (requires Docker). Covers what the
/// InMemory-provider tests in <see cref="BudgetDbContextTests"/> can't: whether the
/// migration actually applies, and whether constraints like unique indexes are enforced
/// by the real database rather than just declared in the EF model.
/// </summary>
public class BudgetDbContextPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private BudgetDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        return new BudgetDbContext(options);
    }

    [Fact]
    public async Task Migration_Seeds_TenSystemCategories()
    {
        await using var context = CreateContext();

        var count = await context.Categories.CountAsync(c => c.IsSystemDefault);

        Assert.Equal(10, count);
    }

    [Fact]
    public async Task DuplicateEmail_ViolatesUniqueIndex()
    {
        await using (var context = CreateContext())
        {
            context.Users.Add(new User { Email = "duplicate@example.com", PasswordHash = "hash" });
            await context.SaveChangesAsync();
        }

        await using var secondContext = CreateContext();
        secondContext.Users.Add(new User { Email = "duplicate@example.com", PasswordHash = "hash" });

        await Assert.ThrowsAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task DuplicateBudget_ForSameUserMonthAndCategory_ViolatesUniqueIndex()
    {
        int userId;
        await using (var context = CreateContext())
        {
            var user = new User { Email = "budget-owner@example.com", PasswordHash = "hash" };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            userId = user.Id;

            context.Budgets.Add(new Budget { UserId = userId, CategoryId = 1, Month = new DateOnly(2026, 9, 1), PlannedAmount = 300m });
            await context.SaveChangesAsync();
        }

        await using var secondContext = CreateContext();
        secondContext.Budgets.Add(new Budget { UserId = userId, CategoryId = 1, Month = new DateOnly(2026, 9, 1), PlannedAmount = 350m });

        await Assert.ThrowsAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
    }
}
