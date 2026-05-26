namespace CrewJet.Shared.Features.Identity.TenantResolution;

/// <summary>
/// Configuration for tenant resolution. Bound from the <c>Tenant</c> section.
/// </summary>
public sealed class TenantResolutionOptions
{
    public const string SectionName = "Tenant";

    /// <summary>
    /// TenantId used when the host has no resolvable subdomain (localhost, IP, apex domain).
    /// Defaults to <c>"default"</c>. Override per environment via <c>Tenant:DevDefault</c>.
    /// </summary>
    public string DevDefault { get; set; } = "default";

    /// <summary>
    /// The claim type that carries the authoritative tenant identifier in the
    /// application session cookie (minted by CrewJet.Server after OIDC ticket receipt).
    /// This is the value used by the data layer (via ITenantContext) for authenticated users.
    /// </summary>
    public string TenantClaimType { get; set; } = "tenant_id";

    /// <summary>
    /// Repeated claim type used by CrewJet.Auth to enumerate every tenant a user belongs to.
    /// CrewJet.Server consumes these to populate the future "active-tenant selector" UI.
    /// </summary>
    public const string TenantMembershipClaimType = "tenant_membership";
}
