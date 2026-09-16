using BudgetPrevisionnel.Application.Auth;
using BudgetPrevisionnel.Application.BankAccounts;
using BudgetPrevisionnel.Application.BankImport;
using BudgetPrevisionnel.Application.Budgets;
using BudgetPrevisionnel.Application.Calendar;
using BudgetPrevisionnel.Application.Categories;
using BudgetPrevisionnel.Application.Dashboard;
using BudgetPrevisionnel.Application.Forecasting;
using BudgetPrevisionnel.Application.Loans;
using BudgetPrevisionnel.Application.RecurringExpenses;
using BudgetPrevisionnel.Application.RecurringIncomes;
using BudgetPrevisionnel.Application.SavingsGoals;
using BudgetPrevisionnel.Application.Transactions;
using BudgetPrevisionnel.Application.Users;
using BudgetPrevisionnel.Infrastructure.Auth;
using BudgetPrevisionnel.Infrastructure.BankImport.BoursoBank;
using BudgetPrevisionnel.Infrastructure.Persistence;
using BudgetPrevisionnel.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetPrevisionnel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BudgetDatabase")
            ?? throw new InvalidOperationException("Connection string 'BudgetDatabase' is not configured.");

        services.AddDbContext<BudgetDbContext>(options => options.UseNpgsql(connectionString));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<AuthService>();

        services.AddScoped<IBankAccountRepository, BankAccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryRuleRepository, CategoryRuleRepository>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
        services.AddScoped<IBankStatementParser, BoursoBankCsvParser>();
        services.AddScoped<BankStatementImportService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<CategoryRuleService>();
        services.AddScoped<BankAccountService>();
        services.AddScoped<TransactionService>();

        services.AddScoped<ISavingsGoalRepository, SavingsGoalRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<IRecurringExpenseRepository, RecurringExpenseRepository>();
        services.AddScoped<IRecurringIncomeRepository, RecurringIncomeRepository>();
        services.AddScoped<SavingsGoalService>();
        services.AddScoped<LoanService>();
        services.AddScoped<RecurringExpenseService>();
        services.AddScoped<RecurringIncomeService>();

        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<BudgetService>();
        services.AddScoped<ForecastService>();
        services.AddScoped<CalendarService>();

        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<DashboardService>();

        return services;
    }
}
