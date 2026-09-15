using System.Net.Http.Headers;
using System.Net.Http.Json;
using BudgetPrevisionnel.Api.Contracts.Auth;

namespace BudgetPrevisionnel.Api.Tests;

internal static class AuthTestHelper
{
    /// <summary>Registers a brand-new user (unique email per call) and returns an
    /// HttpClient with its bearer token already attached, ready for authenticated
    /// requests. Each test gets its own isolated user rather than sharing one and
    /// needing cleanup between tests.</summary>
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(this CustomWebApplicationFactory factory)
    {
        var client = factory.CreateHttpsClient();

        var email = $"test-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "password123"));
        response.EnsureSuccessStatusCode();

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        return client;
    }
}
