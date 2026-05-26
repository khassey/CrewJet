using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CrewJet.Shared.Features.Identity.TenantResolution;

/// <summary>
/// Composition root helpers for tenant resolution. Call
/// <see cref="AddTenantResolution"/> from <c>ConfigureServices</c> and
/// <see cref="UseTenantResolution"/> from the pipeline (before <c>UseAuthentication</c>).
/// </summary>
public static class TenantResolutionExtensions
{
    /// <summary>
    /// Registers tenant-resolution services. The same scoped <see cref="TenantContext"/>
    /// instance backs both <see cref="ITenantContext"/> (secure, claim-derived) and
    /// <see cref="ITenantHint"/> (subdomain hint) — see <see cref="TenantContext"/>
    /// for the rationale.
    ///
    /// The authoritative tenant claim is turned into the active context value via
    /// an <see cref="Microsoft.AspNetCore.Authentication.IClaimsTransformation"/>
    /// registered in the Server host (not here) so the data layer always sees the
    /// claim-derived tenant for authenticated users.
    /// </summary>
    public static IServiceCollection AddTenantResolution(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TenantResolutionOptions>(configuration.GetSection(TenantResolutionOptions.SectionName));

        // One scoped instance per request, exposed under both interface contracts.
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantHint>(sp => sp.GetRequiredService<TenantContext>());

        services.AddSingleton<ITenantResolver, SubdomainTenantResolver>();

        return services;
    }

    /// <summary>
    /// Inserts <see cref="TenantResolutionMiddleware"/> into the pipeline. Place this
    /// before <c>UseRouting</c>/<c>UseAuthentication</c> so downstream components see
    /// the resolved tenant hint (subdomain path for discovery and OIDC initiation).
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
