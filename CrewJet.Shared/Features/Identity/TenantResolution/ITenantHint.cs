namespace CrewJet.Shared.Features.Identity.TenantResolution;

/// <summary>
/// A <b>pre-authentication</b> hint about which tenant the request might be
/// targeting, derived from the host header by <see cref="TenantResolutionMiddleware"/>.
/// This is NEVER trustworthy as a data-access tenant — anyone can type any
/// subdomain into a browser. Use it only for:
///
/// <list type="bullet">
///   <item>Routing / rewrite decisions (apex vs tenant host).</item>
///   <item>Tenant-existence checks on the <c>/login</c> path.</item>
///   <item>Stashing the "initiation tenant" across the OIDC round-trip so the
///         Server BFF can mint the secure <c>tenant_id</c> claim.</item>
/// </list>
///
/// For everything else, use <see cref="ITenantContext"/>, which is set only
/// after the authenticated claim has been applied.
/// </summary>
public interface ITenantHint
{
    /// <summary>
    /// The tenant id parsed from the request host (e.g. <c>acme</c> for
    /// <c>acme.crewjet.io</c>). May be the configured dev default for apex
    /// / localhost / IP hosts. Null until the resolution middleware runs.
    /// </summary>
    string? TenantSubdomain { get; }

    /// <summary>True once <see cref="TenantSubdomain"/> has been populated.</summary>
    bool HasTenantSubdomain { get; }

    /// <summary>
    /// Called by <see cref="TenantResolutionMiddleware"/> exactly once per request.
    /// Subsequent calls with the same value are no-ops; a different value throws.
    /// </summary>
    void Set(string subdomain);
}
