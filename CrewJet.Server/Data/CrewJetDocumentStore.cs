using CrewJet.Shared.Features.Identity.TenantResolution;
using JasperFx;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CrewJet.Server.Data;

/// <summary>
/// Marten document-store composition root. Configures multi-tenant persistence
/// (single DB, <c>tenant_id</c> column on every document) and registers a scoped
/// <see cref="IDocumentSession"/> bound to the request's resolved tenant.
/// </summary>
public static class CrewJetDocumentStore
{
    private const string ConnectionStringName = "CrewJetDatabase";

    public static IServiceCollection AddCrewJetMarten(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connection = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' not found. Add it to appsettings or user secrets.");

        services.AddMarten(opts =>
        {
            opts.Connection(connection);

            // Every document gets a tenant_id column and queries are tenant-filtered automatically.
            opts.Policies.AllDocumentsAreMultiTenanted();

            // System.Text.Json keeps the dependency footprint small; switch to Newtonsoft if we
            // later need polymorphism or other features it provides.
            opts.UseSystemTextJsonForSerialization();

            // Auto-create schema/tables in dev. In prod we'll use explicit migrations.
            opts.AutoCreateSchemaObjects = environment.IsDevelopment() ? AutoCreate.All : AutoCreate.None;
        });

        // Tenant-scoped session per request. Resolving IDocumentSession outside an HTTP request
        // scope (background workers, etc.) will throw — fine for now, revisit when we add them.
        services.AddScoped<IDocumentSession>(sp =>
        {
            var store = sp.GetRequiredService<IDocumentStore>();
            var tenantContext = sp.GetRequiredService<ITenantContext>();
            return store.LightweightSession(tenantContext.TenantId);
        });

        return services;
    }
}
