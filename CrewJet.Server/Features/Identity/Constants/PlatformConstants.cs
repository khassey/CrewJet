namespace CrewJet.Server.Features.Identity.Constants;

/// <summary>
/// Platform-wide identity constants shared across tenant resolution, authorization,
/// and admin portal routing.
/// </summary>
public static class PlatformConstants
{
    /// <summary>
    /// Sentinel TenantId used for the super-admin portal (admin.crewjet.io).
    /// Treated as a regular tenant by resolution, but special-cased by authorization
    /// to gate elevated access and impersonation features.
    /// </summary>
    public const string AdminTenantId = "admin";
}
