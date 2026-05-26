using System.Security.Claims;
using CrewJet.Shared.Features.Identity;
using CrewJet.Shared.Features.Identity.TenantResolution;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CrewJet.Auth.Features.Identity;

/// <summary>
/// Produces a ClaimsPrincipal suitable for signing into the Auth server's own
/// cookie scheme after local login or registration.
///
/// Tenant memberships are emitted as repeated <c>tenant_membership</c> claims.
/// Downstream (the OIDC <c>/connect/authorize</c> handler) is responsible for
/// projecting these into the OIDC token / userinfo response so CrewJet.Server
/// can consume them during ticket receipt.
/// </summary>
public static class IdentityClaims
{
    public static ClaimsPrincipal CreatePrincipalForUser(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.SubjectId),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
            new(ClaimTypes.Email, user.Email),
        };

        foreach (var tenantId in user.Tenants)
            claims.Add(new Claim(TenantResolutionOptions.TenantMembershipClaimType, tenantId.ToString()));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
