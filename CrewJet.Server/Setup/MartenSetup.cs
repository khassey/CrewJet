using CrewJet.Shared.Features.Identity;
using CrewJet.Shared.Features.Identity.TenantResolution;
using JasperFx;
using JasperFx.MultiTenancy;
using Marten;

namespace CrewJet.Server.Setup;

/// <summary>
/// Marten document-store composition root. Configures multi-tenant persistence
/// (single DB, <c>tenant_id</c> column on every document) and registers a scoped
/// <see cref="IDocumentSession"/> bound to the <b>authenticated</b> tenant.
///
/// <para>
/// The scoped session reads <see cref="ITenantContext.TenantId"/>, which throws
/// when no authenticated tenant claim is present. Anonymous endpoints that
/// resolve <c>IDocumentSession</c> by accident fail loudly here rather than
/// silently opening a session against a subdomain-derived id. Pre-authentication
/// flows (login lookup, registration writes) open their own sessions explicitly
/// via the injected <see cref="IDocumentStore"/>.
/// </para>
/// </summary>
public static class MartenSetup
{
    private const string ConnectionStringName = "CrewJetDatabase";

    public static IServiceCollection AddMartenPersistence(
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
            opts.UseSystemTextJsonForSerialization();
            opts.AutoCreateSchemaObjects = environment.IsDevelopment() ? AutoCreate.All : AutoCreate.None;

            opts.Policies.AllDocumentsAreMultiTenanted();
            opts.TenantIdStyle = TenantIdStyle.ForceLowerCase;

            // Platform-level documents that exist OUTSIDE any tenant partition.
            // The user directory is the cross-tenant credential store; the Tenant
            // registry document is what the existence guard reads on the Server.
            //
            // Identity(x => x.Id) is the explicit hook telling Marten the Id is a
            // strongly-typed struct (UserId / TenantId / UserProfileId — all wrap Guid).
            // Marten 7+ recognizes these via reflection, but the explicit declaration
            // documents intent and protects against future auto-detection changes.
            opts.Schema.For<User>()
                .Identity(x => x.Id)
                .SingleTenanted();

            opts.Schema.For<Tenant>()
                .Identity(x => x.Id)
                .SingleTenanted();

            // Tenanted document — Identity config still required for the typed Id.
            opts.Schema.For<UserProfile>()
                .Identity(x => x.Id);
        });

        // Tenant-scoped session per request, bound to the authenticated tenant.
        // ITenantContext.TenantId throws when no claim is present — that is
        // the locked-down contract: only authenticated code paths get a session.
        services.AddScoped<IDocumentSession>(sp =>
        {
            var store = sp.GetRequiredService<IDocumentStore>();
            var tenantContext = sp.GetRequiredService<ITenantContext>();
            return store.LightweightSession(tenantContext.TenantId.ToString());
        });

        return services;
    }
}
