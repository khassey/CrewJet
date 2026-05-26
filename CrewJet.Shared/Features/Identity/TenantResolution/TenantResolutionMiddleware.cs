using Microsoft.AspNetCore.Http;

namespace CrewJet.Shared.Features.Identity.TenantResolution;

/// <summary>
/// Populates <see cref="ITenantHint"/> from the request host at the very start
/// of the pipeline. This is the <b>pre-authentication</b> hint — it tells
/// downstream code which subdomain the user is on, NOT which tenant they're
/// authorized for.
///
/// The authoritative tenant (used by the data layer) is set later in the
/// pipeline by the Server's claims transformation, which writes to
/// <see cref="ITenantContext"/> after the principal is authenticated.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string HttpContextItemKey = "TenantId";

    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver, ITenantHint hint)
    {
        var tenantId = await resolver.ResolveAsync(context);

        hint.Set(tenantId);
        context.Items[HttpContextItemKey] = tenantId;

        await next(context);
    }
}
