using CrewJet.Shared.Features.Identity;
using System.Security.Claims;
using CrewJet.Shared.Features.Identity.TenantResolution;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CrewJet.Server.Features.Identity;

/// <summary>
/// Runs as an <see cref="IClaimsTransformation"/> on every authenticated request.
/// Reads the configured tenant claim from the principal (minted by CrewJet.Server
/// after OIDC ticket receipt in <c>OnTicketReceived</c>) and calls
/// <see cref="ITenantContext.SetFromAuthenticatedClaim"/> so the data layer sees
/// the claim-derived tenant.
///
/// Defensive on the claim value. If the claim is absent (anonymous request that
/// somehow got here) or unparseable (stale cookie from before the strongly-typed
/// Id rename — value is a slug like "acme" instead of a Guid), we leave the
/// secure context unset. The next data-layer access will throw from
/// <see cref="ITenantContext.TenantId"/> with a clear message, and the next OIDC
/// round-trip will mint a fresh claim with the correct shape.
/// </summary>
public sealed class AuthenticatedTenantClaimsTransformation(
    ITenantContext tenantContext,
    IOptions<TenantResolutionOptions> options) : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return Task.FromResult(principal);

        var claimType = options.Value.TenantClaimType;
        var tenantClaim = principal.FindFirst(claimType)?.Value;

        if (!TenantId.TryFrom(tenantClaim, out var tenantId))
            return Task.FromResult(principal);

        // Idempotent: setting the same value twice is a no-op.
        tenantContext.SetFromAuthenticatedClaim(tenantId);
        return Task.FromResult(principal);
    }
}
