using Microsoft.Extensions.Options;

namespace CrewJet.Server.Features.Identity.TenantResolution;

/// <summary>
/// Resolves the active <c>TenantId</c> from the request's host header by extracting the
/// first DNS label (e.g. <c>acme-electric.crewjet.io</c> → <c>acme-electric</c>).
/// On dev hosts (localhost, IP, apex), falls back to the configured dev default and
/// honours an optional <c>?tenant=</c> query-string override for local multi-tenant testing.
/// </summary>
public sealed class SubdomainTenantResolver(IOptions<TenantResolutionOptions> options) : ITenantResolver
{
    private const string TenantQueryKey = "tenant";

    public ValueTask<string> ResolveAsync(HttpContext context)
    {
        var devDefault = options.Value.DevDefault;
        var fromHost = Parse(context.Request.Host.Host, devDefault);

        // Only consult the query-string override when host resolution would otherwise
        // pick the dev fallback — production subdomains always win.
        if (fromHost != devDefault || !context.Request.Query.TryGetValue(TenantQueryKey, out var overrideValue))
            return ValueTask.FromResult(fromHost);

        var candidate = overrideValue.ToString().Trim().ToLowerInvariant();

        return ValueTask.FromResult(!string.IsNullOrEmpty(candidate)
            ? candidate
            : fromHost);
    }

    /// <summary>
    /// Host-only resolution rule, factored out so unit tests can exercise it without an HttpContext.
    /// </summary>
    internal static string Parse(string? host, string devDefault)
    {
        if (string.IsNullOrWhiteSpace(host))
            return devDefault;

        // Less than 3 labels = no real subdomain (localhost, apex domain, raw IP).
        var labels = host.Split('.');

        if (labels.Length < 3)
            return devDefault;

        var first = labels[0];
        return string.IsNullOrWhiteSpace(first) ? devDefault : first.ToLowerInvariant();
    }
}
