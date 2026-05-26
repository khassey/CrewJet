namespace CrewJet.Shared.Features.Identity.TenantResolution;

/// <summary>
/// Scoped implementation of both <see cref="ITenantContext"/> (the secure,
/// claim-derived view) and <see cref="ITenantHint"/> (the subdomain hint).
/// The two interfaces deliberately expose disjoint surfaces:
///
/// <list type="bullet">
///   <item>The Marten session factory and any tenant-isolated code injects
///         <see cref="ITenantContext"/>; accessing <c>TenantId</c> before the
///         claims transformation has run throws.</item>
///   <item>The handful of pre-authentication consumers (routing, OIDC
///         initiation, <c>/login</c> tenant-existence check) inject
///         <see cref="ITenantHint"/> instead.</item>
/// </list>
///
/// Both interfaces resolve to the same scoped instance — there is one piece
/// of per-request state, just two views over it with different trust levels.
/// </summary>
public class TenantContext : ITenantContext, ITenantHint
{
    private string? _tenantSubdomainHint;
    private TenantId? _authenticatedTenantId;

    // ----------- ITenantHint (subdomain-derived) -----------

    public string? TenantSubdomain => _tenantSubdomainHint;
    public bool HasTenantSubdomain => _tenantSubdomainHint is not null;

    public void Set(string subdomain)
    {
        if (string.IsNullOrWhiteSpace(subdomain))
            throw new ArgumentException("Subdomain tenant hint cannot be null or whitespace.", nameof(subdomain));

        if (_tenantSubdomainHint is not null)
        {
            if (string.Equals(_tenantSubdomainHint, subdomain, StringComparison.Ordinal))
                return;

            throw new InvalidOperationException("Tenant subdomain hint already set with a different value.");
        }

        _tenantSubdomainHint = subdomain;
    }

    // ----------- ITenantContext (authenticated, claim-derived) -----------

    public TenantId TenantId =>
        _authenticatedTenantId
        ?? throw new InvalidOperationException(
            "TenantId accessed without an authenticated tenant claim. " +
            "Either the request is anonymous, or AuthenticatedTenantClaimsTransformation has not run yet. " +
            "If you intentionally need the subdomain-derived value, inject ITenantHint instead.");

    public bool IsAuthenticated => _authenticatedTenantId is not null;

    public void SetFromAuthenticatedClaim(TenantId tenantId)
    {
        if (_authenticatedTenantId is not null)
        {
            if (_authenticatedTenantId == tenantId)
                return;

            throw new InvalidOperationException("Authenticated tenant already set with a different value.");
        }

        _authenticatedTenantId = tenantId;
    }
}
