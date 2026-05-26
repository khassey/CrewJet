namespace CrewJet.Shared.Features.Identity.TenantResolution;

/// <summary>
/// Per-request holder for the active <c>TenantId</c>, populated by
/// <see cref="TenantResolutionMiddleware"/> early in the pipeline and consumed by
/// downstream services (Marten sessions, claims transformation, authorization).
/// Scoped lifetime: one instance per HTTP request.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The resolved TenantId for the current request. Throws if accessed before
    /// resolution has run — guard with <see cref="IsResolved"/> if uncertain.
    /// </summary>
    string TenantId { get; }

    /// <summary>
    /// True once <see cref="SetTenantId"/> has been called this request.
    /// </summary>
    bool IsResolved { get; }

    /// <summary>
    /// Set the TenantId for this request. Called exactly once per request by
    /// <see cref="TenantResolutionMiddleware"/>; calling twice throws.
    /// </summary>
    void SetTenantId(string tenantId);
}
