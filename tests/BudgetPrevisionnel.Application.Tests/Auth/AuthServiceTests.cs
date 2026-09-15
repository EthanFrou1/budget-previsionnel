using BudgetPrevisionnel.Application.Auth;

namespace BudgetPrevisionnel.Application.Tests.Auth;

public class AuthServiceTests
{
    private static AuthService CreateService() =>
        new(new FakeUserRepository(), new FakePasswordHasher(), new FakeJwtTokenGenerator());

    [Fact]
    public async Task RegisterAsync_NewEmail_ReturnsTokenForCreatedUser()
    {
        var service = CreateService();

        var result = await service.RegisterAsync("Test@Example.com", "password123");

        Assert.Equal("test@example.com", result.Email);
        Assert.Equal($"fake-token-for-{result.UserId}", result.Token);
    }

    [Fact]
    public async Task RegisterAsync_EmailAlreadyRegistered_Throws()
    {
        var service = CreateService();
        await service.RegisterAsync("test@example.com", "password123");

        await Assert.ThrowsAsync<EmailAlreadyInUseException>(
            () => service.RegisterAsync("test@example.com", "anotherPassword"));
    }

    [Fact]
    public async Task RegisterAsync_EmailDiffersOnlyByCaseOrWhitespace_IsTreatedAsDuplicate()
    {
        var service = CreateService();
        await service.RegisterAsync("test@example.com", "password123");

        await Assert.ThrowsAsync<EmailAlreadyInUseException>(
            () => service.RegisterAsync("  Test@Example.com  ", "anotherPassword"));
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ReturnsToken()
    {
        var service = CreateService();
        await service.RegisterAsync("test@example.com", "password123");

        var result = await service.LoginAsync("test@example.com", "password123");

        Assert.Equal("test@example.com", result.Email);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsInvalidCredentials()
    {
        var service = CreateService();
        await service.RegisterAsync("test@example.com", "password123");

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.LoginAsync("test@example.com", "wrong-password"));
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsSameExceptionAsWrongPassword()
    {
        // Same exception for "no such account" and "wrong password" so the API
        // response can't be used to enumerate which emails have an account.
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.LoginAsync("nobody@example.com", "whatever"));
    }
}
