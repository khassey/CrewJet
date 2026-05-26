using System.Security.Claims;
using CrewJet.Shared.Features.Identity.TenantResolution;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace CrewJet.Auth.Features.Identity;

/// <summary>
/// Minimal-API endpoints that complete OpenIddict's authorization-code flow
/// (passthrough mode). They are deliberately thin — credential collection and
/// user lookup happen on the static-SSR <c>/login</c> page; this layer is only
/// responsible for translating an authenticated cookie session into an OIDC
/// ticket the client (CrewJet.Server BFF) can consume.
///
/// <para>
/// <b>Trust boundary.</b> By the time <see cref="OpenIddictServerAspNetCoreHelpers.GetOpenIddictServerRequest"/>
/// returns a non-null request, OpenIddict's built-in validation pipeline has
/// already enforced the protocol-level guarantees we depend on:
/// </para>
/// <list type="bullet">
///   <item><c>client_id</c> resolves to a registered application and the
///         secret matched (for confidential clients).</item>
///   <item><c>redirect_uri</c> exactly matches one of the URIs registered on
///         the client (no wildcard / substring matching).</item>
///   <item><c>response_type</c> is permitted for the client and the configured
///         grant types (we allow only Authorization Code).</item>
///   <item><c>code_challenge</c> + <c>code_challenge_method</c> are present
///         when PKCE is required (it is — <c>RequireProofKeyForCodeExchange</c>).</item>
///   <item>Requested scopes are subset of the client's permitted scope set.</item>
/// </list>
/// <para>
/// The handlers below add only the policy on top of that: which claims to mint,
/// where each claim is allowed to ride, and whether a logout request is acting
/// on the same subject as the cookie session.
/// </para>
/// </summary>
public static class OidcEndpoints
{
    public static IEndpointRouteBuilder MapOidcEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/connect/authorize", ["GET", "POST"], AuthorizeAsync);
        endpoints.MapPost("/connect/token", TokenAsync);
        endpoints.MapMethods("/connect/userinfo", ["GET", "POST"], (Delegate)UserInfoAsync); // Cast to Delegate so the route handler keeps the IResult return value.
        endpoints.MapMethods("/connect/logout", ["GET", "POST"], LogoutAsync);

        return endpoints;
    }

    // -------------------------------------------------------------------
    // /connect/authorize
    // If the user isn't signed in to the Auth cookie scheme, redirect to
    // /login with returnUrl pointing back here so the flow resumes after
    // credentials are entered. Otherwise issue the auth code.
    // -------------------------------------------------------------------
    private static async Task AuthorizeAsync(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
                      ?? throw new InvalidOperationException("OpenIddict authorization request not found.");

        // Defense in depth. OpenIddict has already validated the grant against
        // the client's permitted response types; we additionally refuse anything
        // that isn't the auth-code flow (the only flow this server supports).
        if (!request.IsAuthorizationCodeFlow())
        {
            await context.ChallengeAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnsupportedResponseType,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Only the authorization code flow is supported."
                }));
            return;
        }

        var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!result.Succeeded || result.Principal?.Identity?.IsAuthenticated != true)
        {
            await context.ChallengeAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new AuthenticationProperties
                {
                    RedirectUri = context.Request.PathBase + context.Request.Path + context.Request.QueryString
                });
            return;
        }

        var cookiePrincipal = result.Principal;
        var subject = cookiePrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // A cookie session without a subject is an internal contract violation
        // (Login/Register always set NameIdentifier). Treat it as unauthenticated.
        if (string.IsNullOrEmpty(subject))
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await context.ChallengeAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new AuthenticationProperties
                {
                    RedirectUri = context.Request.PathBase + context.Request.Path + context.Request.QueryString
                });
            return;
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name,
            Claims.Role);

        identity.AddClaim(Claims.Subject, subject);

        var email = cookiePrincipal.FindFirst(ClaimTypes.Email)?.Value;
        if (!string.IsNullOrEmpty(email)) identity.AddClaim(Claims.Email, email);

        var name = cookiePrincipal.FindFirst(ClaimTypes.Name)?.Value;
        if (!string.IsNullOrEmpty(name)) identity.AddClaim(Claims.Name, name);

        foreach (var membership in cookiePrincipal.FindAll(TenantResolutionOptions.TenantMembershipClaimType))
            identity.AddClaim(TenantResolutionOptions.TenantMembershipClaimType, membership.Value);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        foreach (var claim in principal.Claims)
            claim.SetDestinations(GetDestinations(claim, principal));

        await context.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, principal);
    }

    // -------------------------------------------------------------------
    // /connect/token
    // Authorization-code exchange: re-authenticate the principal carried in
    // the code (OpenIddict handles this) and re-sign so a new id/access token
    // pair is issued.
    // -------------------------------------------------------------------
    private static async Task TokenAsync(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
                      ?? throw new InvalidOperationException("OpenIddict token request not found.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            throw new InvalidOperationException($"Unsupported grant type: {request.GrantType}");

        var auth = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (!auth.Succeeded || auth.Principal is null)
        {
            await context.ChallengeAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The authorization code is no longer valid."
                }));
            return;
        }

        // Refresh destinations in case scopes changed since the code was minted.
        foreach (var claim in auth.Principal.Claims)
            claim.SetDestinations(GetDestinations(claim, auth.Principal));

        await context.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, auth.Principal);
    }

    // -------------------------------------------------------------------
    // /connect/userinfo
    // OpenIddict authenticates the access token automatically; we just shape
    // the response. Only emit claims the granted scopes allow.
    // -------------------------------------------------------------------
    private static async Task<IResult> UserInfoAsync(HttpContext context)
    {
        var auth = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (!auth.Succeeded || auth.Principal is null)
            return Results.Challenge(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

        var user = auth.Principal;

        var claims = new Dictionary<string, object>
        {
            [Claims.Subject] = user.FindFirst(Claims.Subject)?.Value ?? string.Empty
        };

        if (user.HasScope(Scopes.Email))
        {
            var email = user.FindFirst(Claims.Email)?.Value;
            if (!string.IsNullOrEmpty(email)) claims[Claims.Email] = email;
        }

        if (user.HasScope(Scopes.Profile))
        {
            var name = user.FindFirst(Claims.Name)?.Value;
            if (!string.IsNullOrEmpty(name)) claims[Claims.Name] = name;
        }

        // Tenant memberships ride the custom "crewjet:tenant" scope. Clients that
        // didn't request it (or don't have permission to) don't see them.
        if (!user.HasScope(CrewJetScopes.Tenant))
            return Results.Json(claims);

        var memberships = user
            .FindAll(TenantResolutionOptions.TenantMembershipClaimType)
            .Select(c => c.Value)
            .ToArray();

        if (memberships.Length > 0)
            claims[TenantResolutionOptions.TenantMembershipClaimType] = memberships;

        return Results.Json(claims);
    }

    // -------------------------------------------------------------------
    // /connect/logout — RP-initiated logout (end_session).
    //
    // OpenIddict has already validated post_logout_redirect_uri against the
    // client's registered URIs by the time we run, and parsed id_token_hint
    // into a principal accessible via AuthenticateAsync(OpenIddictServerScheme).
    //
    // We sub-match the hint against the cookie session to refuse cross-subject
    // logouts (a crafted URL that tries to sign out a different user than the
    // one currently in the browser), then SignOut both schemes. Crucially we
    // do NOT set Properties.RedirectUri — OpenIddict honors the validated
    // post_logout_redirect_uri from the request itself.
    // -------------------------------------------------------------------
    private static async Task LogoutAsync(HttpContext context)
    {
        var cookieAuth = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var hintAuth = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var cookieSub = cookieAuth.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var hintSub = hintAuth.Principal?.FindFirst(Claims.Subject)?.Value;

        // If a hint was supplied AND a cookie session exists, they must agree.
        // A mismatch is either a stale id_token from an old session or a
        // hostile attempt to sign out the wrong principal — either way, refuse.
        if (!string.IsNullOrEmpty(hintSub) &&
            !string.IsNullOrEmpty(cookieSub) &&
            !string.Equals(hintSub, cookieSub, StringComparison.Ordinal))
        {
            await context.ChallengeAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidRequest,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The id_token_hint subject does not match the active session."
                }));
            return;
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // No Properties.RedirectUri here — OpenIddict reads the validated
        // post_logout_redirect_uri from the request and routes accordingly.
        await context.SignOutAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // -------------------------------------------------------------------
    // Destinations decide which token a claim ends up in.
    //
    // Access token: everything that needs to be visible to resource servers /
    //   the BFF for authorization decisions. OpenIddict samples put most
    //   claims here by default; we follow that convention.
    //
    // Identity token: only scope-gated user-identity claims (profile, email)
    //   plus our custom tenant_membership which rides the "crewjet:tenant"
    //   scope. Clients that didn't ask for those scopes don't get them.
    // -------------------------------------------------------------------
    private static IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
    {
        switch (claim.Type)
        {
            case Claims.Subject:
                // Sub is mandatory in both tokens by spec.
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            case Claims.Name:
            case Claims.PreferredUsername:
                yield return Destinations.AccessToken;

                if (principal.HasScope(Scopes.Profile))
                    yield return Destinations.IdentityToken;

                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;

                if (principal.HasScope(Scopes.Email))
                    yield return Destinations.IdentityToken;

                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;

                if (principal.HasScope(Scopes.Roles))
                    yield return Destinations.IdentityToken;

                yield break;

            case TenantResolutionOptions.TenantMembershipClaimType:
                // Tenant memberships are sensitive workspace metadata — gate
                // them behind the custom crewjet:tenant scope so clients that
                // didn't request it don't receive the list.
                //
                // Access-token only. UserInfoAsync reads memberships from the
                // access-token principal and emits them in the /connect/userinfo
                // response; the BFF Server's OIDC handler picks them up there
                // via ClaimActions.MapJsonKey. Putting them in the id_token too
                // would duplicate every claim on the Server-side principal
                // (id_token path + userinfo path, MapJsonKey doesn't dedupe).
                if (!principal.HasScope(CrewJetScopes.Tenant))
                    yield break;

                yield return Destinations.AccessToken;
                yield break;

            default:
                // Default: access token only. Unknown claims should not leak
                // into the id_token without an explicit scope gate above.
                yield return Destinations.AccessToken;
                yield break;
        }
    }

    /// <summary>Custom CrewJet OIDC scopes. Mirrors the server-side registration.</summary>
    private static class CrewJetScopes
    {
        public const string Tenant = "crewjet:tenant";
    }
}
