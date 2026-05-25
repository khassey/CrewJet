namespace CrewJet.Server.Features.Identity.TenantResolution;

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
}
