namespace BudgetPrevisionnel.Api.Tests;

/// <summary>Shares one CustomWebApplicationFactory (and its one Postgres container)
/// across every test class below - starting a fresh container per class would work but
/// costs real seconds per class for no benefit, since each test already uses a unique
/// email/dataset and needs no cleanup between tests.</summary>
[CollectionDefinition(Name)]
public sealed class ApiTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "Api integration tests";
}
