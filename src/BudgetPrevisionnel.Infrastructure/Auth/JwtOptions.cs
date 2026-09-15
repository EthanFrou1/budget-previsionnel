namespace BudgetPrevisionnel.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    // Never committed: dotnet user-secrets in Development, an environment
    // variable / secret manager elsewhere. Program.cs fails fast if it's empty.
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}
