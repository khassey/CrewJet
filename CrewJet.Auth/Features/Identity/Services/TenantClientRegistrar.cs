using CrewJet.Shared.Features.Identity;
using OpenIddict.Abstractions;

namespace CrewJet.Auth.Features.Identity.Services;

/// <summary>
/// Keeps the OpenIddict <c>crewjet-server</c> client application's redirect URIs
/// in sync with the set of registered tenants. Each tenant has a dedicated
/// subdomain host (e.g. <c>acme.crewjet.test:7168</c>) and therefore a distinct
/// <c>/signin-oidc</c> and <c>/signout-callback-oidc</c> redirect URI that
/// OpenIddict must accept.
///
/// Called from two places:
///   * <see cref="RegistrationService"/> after a new tenant is created — so the
///     freshly registered user can complete OIDC immediately.
///   * <see cref="Seeding.OpenIddictSeeder"/> on startup — to backfill all
///     existing tenants in case the client app was created before they were.
/// </summary>
public sealed class TenantClientRegistrar(
    IOpenIddictApplicationManager applicationManager,
    IConfiguration configuration)
{
    public const string CrewJetServerClientId = "crewjet-server";

    private const int MaxConcurrencyRetries = 5;

    public async Task EnsureRedirectsForTenantAsync(string subdomain, CancellationToken ct = default)
    {
        // The PopulateAsync → mutate → UpdateAsync pattern is a check-then-act
        // window. OpenIddict's EF storage carries a ConcurrencyToken column, so
        // concurrent writers see one another's commits, and the loser throws
        // OpenIddictExceptions.ConcurrencyException on UpdateAsync. We retry up
        // to N times because the mutation is a set-union (HashSet.Add): redoing
        // against the post-conflict state still converges on the right end-state.

        var format = configuration["App:ServerHostFormat"]
                     ?? throw new InvalidOperationException("App:ServerHostFormat is required to compute tenant redirect URIs.");

        var baseUri = string.Format(format, subdomain).TrimEnd('/');
        var signin = new Uri(baseUri + "/signin-oidc");
        var signout = new Uri(baseUri + "/signout-callback-oidc");

        for (var attempt = 1;; attempt++)
        {
            var application = await applicationManager.FindByClientIdAsync(CrewJetServerClientId, ct)
                              ?? throw new InvalidOperationException($"OpenIddict client '{CrewJetServerClientId}' is not registered.");

            var descriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(descriptor, application, ct);

            var redirectAdded = descriptor.RedirectUris.Add(signin);
            var postLogoutAdded = descriptor.PostLogoutRedirectUris.Add(signout);

            var changed = redirectAdded || postLogoutAdded;

            // Nothing to write — another writer (or a previous run) already
            // added these URIs. Idempotent exit.
            if (!changed) return;

            try
            {
                await applicationManager.UpdateAsync(application, descriptor, ct);
                return;
            }
            catch (OpenIddictExceptions.ConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                // Re-read the application and re-apply on the next iteration.
                // No backoff: the contention here is bounded by the number of
                // concurrent tenant registrations, which is small.
            }
        }
    }
}
