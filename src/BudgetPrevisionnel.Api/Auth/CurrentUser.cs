using System.IdentityModel.Tokens.Jwt;
using BudgetPrevisionnel.Application.Auth;

namespace BudgetPrevisionnel.Api.Auth;

/// <summary>
/// Reads the authenticated user's identity from the current request's JWT claims.
/// Program.cs sets MapInboundClaims = false on the JWT bearer handler so the "sub"
/// and "email" claim names below survive unchanged instead of being remapped to
/// the long ClaimTypes.* URIs ASP.NET Core uses by default.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public int UserId => int.Parse(RequireClaim(JwtRegisteredClaimNames.Sub));

    public string Email => RequireClaim(JwtRegisteredClaimNames.Email);

    private string RequireClaim(string claimType)
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirst(claimType)?.Value;

        return string.IsNullOrEmpty(value)
            ? throw new InvalidOperationException("No authenticated user in the current request.")
            : value;
    }
}
