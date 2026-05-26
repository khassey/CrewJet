using CrewJet.Auth.Data;
using CrewJet.Auth.Features.Identity.Services;
using CrewJet.Shared.Features.Identity;
using Marten;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CrewJet.Auth.Features.Identity.Seeding;

/// <summary>
/// Seeds the core "crewjet-server" OpenIddict client (the BFF / resource server)
/// on startup, then backfills the per-tenant redirect URIs by walking the
/// platform-wide <see cref="Tenant"/> registry. This keeps the OIDC client
/// happy after restarts, fresh databases, or out-of-band tenant inserts.
/// </summary>
public class OpenIddictSeeder(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<OpenIddictDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        const string clientId = TenantClientRegistrar.CrewJetServerClientId;

        if (await applicationManager.FindByClientIdAsync(clientId, cancellationToken) is null)
        {
            await applicationManager.CreateAsync(
                new OpenIddictApplicationDescriptor
                {
                    ClientId = clientId,
                    ClientSecret = "dev-client-secret-change-in-prod",
                    DisplayName = "CrewJet Server (BFF)",
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.Endpoints.EndSession,
                        Permissions.Endpoints.Revocation,

                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.GrantTypes.RefreshToken,

                        Permissions.ResponseTypes.Code,

                        Permissions.Scopes.Email,
                        Permissions.Scopes.Profile,
                        Permissions.Prefixes.Scope + "crewjet:tenant",
                    },
                    ClientType = ClientTypes.Confidential
                    // Redirect URIs are added per-tenant below.
                }, cancellationToken);
        }

        // Backfill: every tenant in the platform registry contributes a
        // redirect URI to the crewjet-server client.
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var registrar = scope.ServiceProvider.GetRequiredService<TenantClientRegistrar>();

        await using var session = store.QuerySession();

        var tenantSlugs = await session.Query<Tenant>()
            .Select(t => t.Slug)
            .ToListAsync(cancellationToken);

        foreach (var slug in tenantSlugs)
            await registrar.EnsureRedirectsForTenantAsync(slug, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
