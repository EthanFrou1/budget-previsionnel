using BudgetPrevisionnel.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace BudgetPrevisionnel.Api.Tests;

/// <summary>
/// Boots the real Api against an ephemeral Postgres container - same reasoning as the
/// Infrastructure.Tests Testcontainers suite (Lot 1): a real database exercises
/// migrations, constraints and EF query translation that a lighter double can't.
/// Everything else (Jwt:Issuer/Audience, JSON options, Serilog...) runs exactly as it
/// does in production.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ConnectionStringVariable = "ConnectionStrings__BudgetDatabase";
    private const string JwtKeyVariable = "Jwt__Key";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();

        // Environment variables, not WebApplicationFactory's ConfigureAppConfiguration:
        // Program.cs (minimal hosting) reads builder.Configuration directly in its own
        // top-level statements - for AddInfrastructure's connection string AND for the
        // Jwt options used to build TokenValidationParameters - and that happens before
        // ConfigureAppConfiguration's overrides are merged in. The first attempt at this
        // factory used ConfigureAppConfiguration and it silently lost to this machine's
        // real appsettings.Development.json/user-secrets: tests were registering users
        // into the actual local dev database and signing tokens with a key the server
        // validated against the real Jwt:Key, both without any error. Environment
        // variables are read by WebApplication.CreateBuilder(args) itself, with higher
        // precedence than user-secrets, so they reach both reads correctly.
        Environment.SetEnvironmentVariable(ConnectionStringVariable, _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable(JwtKeyVariable, "integration-test-signing-key-never-used-outside-this-test-run-ok");

        // Accessing Services builds the host - it reads the environment variables above.
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable(ConnectionStringVariable, null);
        Environment.SetEnvironmentVariable(JwtKeyVariable, null);

        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Program.cs calls UseHttpsRedirection(), same as production. The default
    /// CreateClient() uses an http:// base address, so every request 307-redirects to
    /// https:// - and HttpClient's automatic redirect handling strips the Authorization
    /// header on a scheme change, silently turning every authenticated call into an
    /// anonymous one. Requesting https:// directly avoids the redirect entirely; no real
    /// TLS is involved since TestServer never leaves the process.
    /// </summary>
    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
}
