using System.Security.Claims;
using CrewJet.Server.Data;
using CrewJet.Server.Features.Identity.Models;
using CrewJet.Server.Features.Identity.Persistence;
using CrewJet.Server.Features.Identity.Services;
using CrewJet.Server.Features.Identity.TenantResolution;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace CrewJet.Server.Features.Identity.Claims;

public sealed class CrewUserClaimsTransformer(
    ICrewUserStore store,
    UserManager<ApplicationUser> userManager,
    UserLinkingService userLinkingService,
    ITenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CrewUserClaimsTransformer> logger) : IClaimsTransformation
{
    private const string HttpContextItemKey = "CrewUser";
    private const string TenantIdClaimType = "crewjet:tenant_id";
    private const string CrewUserIdClaimType = "crewjet:crew_user_id";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true })
            return principal;

        // Idempotent: if claims are already enriched on this principal, skip.
        if (principal.HasClaim(c => c.Type == CrewUserIdClaimType))
            return principal;

        var httpContext = httpContextAccessor.HttpContext;

        // Per-request cache: if another component already loaded the CrewUser this request, reuse it.
        var cached = httpContext?.Items[HttpContextItemKey] as CrewUser;
        var crewUser = cached ?? await LoadAsync(principal);
        if (crewUser is null) return principal;

        // Defense in depth: principal's CrewUser must belong to the resolved tenant for this request.
        // Cookies scoped to *.crewjet.io would otherwise let a session bleed across subdomains.
        if (tenantContext.IsResolved && !string.Equals(crewUser.TenantId, tenantContext.TenantId, StringComparison.Ordinal))
        {
            logger.LogWarning("Tenant mismatch: CrewUser {CrewUserId} belongs to tenant {UserTenant} but request is for {RequestTenant}",
                crewUser.Id, crewUser.TenantId, tenantContext.TenantId);

            return principal;
        }

        if (httpContext is not null)
            httpContext.Items[HttpContextItemKey] = crewUser;

        var clone = principal.Clone();

        if (clone.Identity is not ClaimsIdentity identity)
            return principal;

        identity.AddClaim(new Claim(CrewUserIdClaimType, crewUser.Id.ToString()));
        identity.AddClaim(new Claim(TenantIdClaimType, crewUser.TenantId));

        foreach (var role in crewUser.Roles)
            identity.AddClaim(new Claim(ClaimTypes.Role, role));

        return clone;
    }

    private async Task<CrewUser?> LoadAsync(ClaimsPrincipal principal)
    {
        var externalId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(externalId))
            return null;

        var existing = await store.FindByExternalIdAsync(externalId);
        if (existing is not null) return existing;

        // Lazy provision: principal is authenticated but no CrewUser exists yet.
        // Happens on the first authenticated request after a successful sign-in.
        var identityUser = await userManager.FindByIdAsync(externalId);
        if (identityUser is null) return null;

        return await userLinkingService.GetOrCreateCrewUserAsync(identityUser);
    }
}
