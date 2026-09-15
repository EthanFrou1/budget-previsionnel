using BudgetPrevisionnel.Domain.Entities;
using BudgetPrevisionnel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BudgetPrevisionnel.Infrastructure.Tests.Persistence;

public class BudgetDbContextTests
{
    private static BudgetDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new BudgetDbContext(options);
    }

    [Fact]
    public void Model_Builds_WithoutThrowing()
    {
        // Forces EF to evaluate every IEntityTypeConfiguration in the assembly:
        // catches relationship/mapping mistakes before they hit a real migration.
        using var context = CreateContext(Guid.NewGuid().ToString());

        var model = context.Model;

        Assert.NotNull(model);
    }

    [Fact]
    public async Task Transaction_SavedAndReloaded_KeepsBankAccountAndCategory()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var context = CreateContext(databaseName);

        var user = new User { Email = "test@example.com", PasswordHash = "hash" };
        var category = new Category { Name = "Abonnements & téléphonie", IsSystemDefault = true };
        var account = new BankAccount { User = user, BankName = "BoursoBank", Label = "Compte courant" };
        var transaction = new Transaction
        {
            BankAccount = account,
            Category = category,
            Date = new DateOnly(2026, 9, 1),
            RawLabel = "Riot* CB*2856",
            CleanedLabel = "Riot Games",
            Amount = -10.99m
        };

        context.AddRange(user, category, account, transaction);
        await context.SaveChangesAsync();

        using var reloadContext = CreateContext(databaseName);

        var reloaded = await reloadContext.Transactions
            .Include(t => t.BankAccount)
            .Include(t => t.Category)
            .SingleAsync(t => t.Id == transaction.Id);

        Assert.Equal("Riot Games", reloaded.CleanedLabel);
        Assert.Equal("BoursoBank", reloaded.BankAccount.BankName);
        Assert.Equal("Abonnements & téléphonie", reloaded.Category!.Name);
    }
}
