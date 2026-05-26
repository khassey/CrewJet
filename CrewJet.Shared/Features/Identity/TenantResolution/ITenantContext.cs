namespace CrewJet.Shared.Features.Identity.TenantResolution;

/// <summary>
/// The <b>authoritative</b> tenant identity for the current request. Populated
/// only from the authenticated principal's tenant claim (see the Server-side
/// <c>AuthenticatedTenantClaimsTransformation</c>). Reading <see cref="TenantId"/>
/// before the claim is applied throws — that is intentional: it forces every
/// data-layer caller to either be authenticated or to opt out by reaching for
/// <see cref="ITenantHint"/> explicitly.
///
/// <para>
/// The Marten session factory consumes this interface. Anonymous endpoints
/// that resolve <c>IDocumentSession</c> will fail loudly instead of silently
/// running queries against a subdomain-derived tenant id.
/// </para>
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The authoritative TenantId for the current request, sourced from the
    /// authenticated principal's <c>tenant_id</c> claim.
    /// Throws <see cref="InvalidOperationException"/> if no authenticated
    /// claim has been applied yet (i.e. the request is anonymous, or the
    /// claims transformation has not run).
    /// </summary>
    TenantId TenantId { get; }

    /// <summary>
    /// True once an authenticated claim has populated <see cref="TenantId"/>.
    /// Use this to guard data access in code paths that might be reached
    /// anonymously (rare — most code should just access <see cref="TenantId"/>).
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Called by the claims transformation in CrewJet.Server after OIDC ticket
    /// receipt mints the cookie. Idempotent: setting the same value twice
    /// is a no-op; setting a different value throws.
    /// </summary>
    void SetFromAuthenticatedClaim(TenantId tenantId);
}
