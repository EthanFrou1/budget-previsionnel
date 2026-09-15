using System.Net;
using System.Net.Http.Json;
using BudgetPrevisionnel.Api.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Tests;

[Collection(ApiTestCollection.Name)]
public class AuthEndpointsTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task Register_NewEmail_ReturnsTokenAndUser()
    {
        var client = factory.CreateHttpsClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "password123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(email, body!.User.Email);
        Assert.False(string.IsNullOrEmpty(body.Token));
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409WithProblemDetails()
    {
        var client = factory.CreateHttpsClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "password123"));

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "password123"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Conflict", problem!.Title);
        Assert.Equal(409, problem.Status);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400WithValidationProblemDetails()
    {
        // Proves FluentValidation (not just service-layer checks) runs for this request:
        // "not-an-email" never reaches AuthService at all.
        var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("not-an-email", "password123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Email", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Register_ShortPassword_Returns400WithValidationProblemDetails()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new RegisterRequest($"test-{Guid.NewGuid():N}@example.com", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Password", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = factory.CreateHttpsClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "password123"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUser()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.True(user!.Id > 0);
    }
}
