using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BudgetPrevisionnel.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` at design time (migrations add/update). Kept independent
/// from BudgetPrevisionnel.Api's Program.cs so generating a migration never requires
/// the API's full configuration (e.g. a production connection string) to be present.
/// </summary>
public class BudgetDbContextFactory : IDesignTimeDbContextFactory<BudgetDbContext>
{
    public BudgetDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BUDGET_PREVISIONNEL_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=budget_previsionnel;Username=budget;Password=budget_dev_only";

        var optionsBuilder = new DbContextOptionsBuilder<BudgetDbContext>()
            .UseNpgsql(connectionString);

        return new BudgetDbContext(optionsBuilder.Options);
    }
}
