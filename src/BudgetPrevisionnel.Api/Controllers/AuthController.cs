using BudgetPrevisionnel.Api.Contracts.Auth;
using BudgetPrevisionnel.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BudgetPrevisionnel.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request.Email, request.Password, cancellationToken);
        return Ok(AuthResponse.FromResult(result));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);
        return Ok(AuthResponse.FromResult(result));
    }

    /// <summary>Proves the end-to-end pipeline works: a valid bearer token resolves to a user.</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserResponse> Me() => Ok(new UserResponse(currentUser.UserId, currentUser.Email));
}
