namespace CrewJet.Server.Features.Identity.TenantResolution;

/// <summary>
/// Resolves the active tenant for each request and stores the result in both
/// <see cref="ITenantContext"/> (for DI consumers) and
/// <see cref="HttpContext.Items"/> (for components that prefer direct context access).
/// Must run BEFORE <c>UseAuthentication</c> so the claims transformer and any
/// tenant-scoped Marten session see the correct tenant.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string HttpContextItemKey = "TenantId";

    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver, ITenantContext tenantContext)
    {
        var tenantId = await resolver.ResolveAsync(context);

        tenantContext.SetTenantId(tenantId);
        context.Items[HttpContextItemKey] = tenantId;

        await next(context);
    }
}
