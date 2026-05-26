using Microsoft.AspNetCore.Http;

namespace CrewJet.Shared.Features.Identity.TenantResolution;

/// <summary>
/// Strategy for determining the TenantId of an incoming request. Implementations
/// may inspect the host header, query string, route, claims, or any combination.
/// Invoked by <see cref="TenantResolutionMiddleware"/>.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Resolve the TenantId for <paramref name="context"/>. Must return a non-empty value;
    /// callers should fall back to a configured dev default rather than null/empty.
    /// </summary>
    ValueTask<string> ResolveAsync(HttpContext context);
}
