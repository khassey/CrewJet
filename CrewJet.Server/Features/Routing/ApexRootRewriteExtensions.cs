namespace CrewJet.Server.Features.Routing;

/// <summary>
/// Scaffolding. On the apex (or any non-tenant host) we want the URL bar to
/// show <c>/</c> for both the marketing page and an in-workspace dashboard.
/// Blazor's router maps a single path to a single component, so we rewrite
/// <c>/</c> → <c>/marketing</c> when there is no tenant subdomain. The
/// dashboard owns the real <c>/</c> route and renders on tenant hosts.
///
/// <para>
/// <b>Remove me</b> once marketing moves to its own host (e.g. crewjet.io
/// marketing + *.crewjet.io app). Until then this middleware is the single
/// load-bearing piece that lets the apex and a tenant subdomain coexist at
/// the conceptual root URL. The rewrite is intentionally narrow — it triggers
/// only on the literal path <c>"/"</c>, so static files, API routes, and any
/// other deep links flow through untouched.
/// </para>
/// </summary>
internal static class ApexRootRewriteExtensions
{
    public static IApplicationBuilder UseApexRootToMarketing(
        this IApplicationBuilder app,
        Func<string, bool> isTenantSubdomain)
    {
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/" && !isTenantSubdomain(context.Request.Host.Host))
                context.Request.Path = "/marketing";

            await next();
        });
    }
}
