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
    /// Registers tenant-resolution services: scoped <see cref="ITenantContext"/>,
    /// singleton <see cref="ITenantResolver"/>, and binds <see cref="TenantResolutionOptions"/>
    /// from the <c>Tenant</c> configuration section.
    /// </summary>
    public static IServiceCollection AddTenantResolution(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TenantResolutionOptions>(configuration.GetSection(TenantResolutionOptions.SectionName));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddSingleton<ITenantResolver, SubdomainTenantResolver>();


        return services;
    }

    /// <summary>
    /// Inserts <see cref="TenantResolutionMiddleware"/> into the pipeline. Place this
    /// before <c>UseRouting</c>/<c>UseAuthentication</c> so downstream components see
    /// the resolved tenant.
    /// </summary>
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
