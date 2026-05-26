using CrewJet.Server.Components;
using CrewJet.Server.Features.Routing;
using CrewJet.Server.Setup;
using CrewJet.Shared.Features.Identity;
using CrewJet.Shared.Features.Identity.TenantResolution;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization(options =>
    {
        // The WASM client renders the tenant home and inspects the principal
        // (tenant_id, tenant_membership). Without this, only Name + Role would
        // be serialized through to the browser circuit.
        options.SerializeAllClaims = true;
    });

// === Core foundations ===
builder.Services
    .AddHttpContextAccessor()
    .AddBffAuthentication(builder.Configuration, builder.Environment)
    .AddMartenPersistence(builder.Configuration, builder.Environment)
    .AddAuthorization()
    .AddCascadingAuthenticationState();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseTenantResolution();
app.UseApexRootToMarketing(IsTenantSubdomain);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CrewJet.Client._Imports).Assembly);

// -----------------------------------------------------------------------------
// /login — the cookie LoginPath. Two outcomes depending on tenant existence:
//   * Subdomain has no registered Tenant document → redirect to Auth's
//     /register?subdomain={x} so the visitor can claim it.
//   * Subdomain is a real tenant → fire the OIDC challenge with the original
//     returnUrl so the user lands back where they were trying to go.
// On the apex (no subdomain) there's nothing to log into; bounce to marketing.
// -----------------------------------------------------------------------------
app.MapGet("/login", async (HttpContext ctx, IDocumentStore store, IConfiguration config) =>
{
    var returnUrl = ResolveLocalReturnUrl(ctx.Request.Query);

    if (!IsTenantSubdomain(ctx.Request.Host.Host))
    {
        ctx.Response.Redirect("/");
        return;
    }

    // /login is a pre-authentication route: use the subdomain hint, not the
    // secure claim (which by definition isn't set yet).
    var hint = ctx.RequestServices.GetRequiredService<ITenantHint>();
    var subdomain = hint.HasTenantSubdomain ? hint.TenantSubdomain : null;

    if (string.IsNullOrWhiteSpace(subdomain))
    {
        ctx.Response.Redirect("/");
        return;
    }

    // Tenant.Id is a strongly-typed Guid struct now — look up by Slug, not by primary key.
    await using var session = store.QuerySession();
    var tenantExists = await session.Query<Tenant>().AnyAsync(t => t.Slug == subdomain);

    if (!tenantExists)
    {
        var authority = config["Auth:Authority"]?.TrimEnd('/') ?? string.Empty;
        ctx.Response.Redirect($"{authority}/register?subdomain={Uri.EscapeDataString(subdomain)}");
        return;
    }

    await ctx.ChallengeAsync(
        OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = returnUrl });
}).AllowAnonymous();

// -----------------------------------------------------------------------------
// /logout — federated sign-out.
// Clears the BFF application cookie, then SignOutAsync(OIDC) which redirects
// the browser to CrewJet.Auth's /connect/logout (end_session) endpoint with an
// id_token_hint. Auth clears its own cookie session and bounces back via
// /signout-callback-oidc, landing the user at "/".
//
// Antiforgery is intentionally disabled here. The form post originates from
// the WASM Dashboard, which has no clean way to attach an antiforgery token
// (AntiforgeryStateProvider needs a server-rendered seed it doesn't get on a
// pure-WASM page). The CSRF blast radius is low — worst case a third-party
// forces a logout — and the endpoint requires an authenticated cookie to do
// anything at all. Acceptable trade-off until a CSRF-safe logout path exists.
// -----------------------------------------------------------------------------
app.MapPost("/logout", async ctx =>
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // No Properties.RedirectUri — the OIDC handler builds the redirect to
        // Auth's end_session endpoint using post_logout_redirect_uri configured
        // on the OIDC options (SignedOutCallbackPath).
        await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
    })
    .RequireAuthorization()
    .DisableAntiforgery();

// -----------------------------------------------------------------------------
// Minimal JSON whoami for testing the OIDC + claim enrichment flow.
// -----------------------------------------------------------------------------
app.MapGet("/api/whoami", (ClaimsPrincipal user, ITenantContext tenantContext, ITenantHint hint) =>
{
    var tenantClaim = user.FindFirst("tenant_id")?.Value;
    var subject = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
    var email = user.FindFirst(ClaimTypes.Email)?.Value;
    var memberships = user
        .FindAll(TenantResolutionOptions.TenantMembershipClaimType)
        .Select(c => c.Value)
        .ToArray();

    return Results.Ok(new
    {
        user.Identity?.IsAuthenticated,
        Subject = subject,
        Email = email,
        TenantIdFromClaim = tenantClaim,
        SecureTenantId = tenantContext.IsAuthenticated ? tenantContext.TenantId.ToString() : null,
        SubdomainHint = hint.HasTenantSubdomain ? hint.TenantSubdomain : null,
        TenantMemberships = memberships
    });
}).RequireAuthorization();

app.Run();

// -----------------------------------------------------------------------------
// Helpers
// -----------------------------------------------------------------------------
static bool IsTenantSubdomain(string host)
{
    if (string.IsNullOrWhiteSpace(host)) return false;

    var bare = host.Trim().TrimEnd('.');
    var labels = bare.Split('.', StringSplitOptions.RemoveEmptyEntries);

    if (labels.Length < 3) return false; // apex / localhost / IP
    if (labels[0].Equals("admin", StringComparison.OrdinalIgnoreCase)) return false;
    if (labels[0].Equals("www", StringComparison.OrdinalIgnoreCase)) return false;

    return true;
}

static string ResolveLocalReturnUrl(IQueryCollection query)
{
    // Cookie middleware uses "ReturnUrl"; manual links sometimes use "returnUrl".
    var candidate = query["ReturnUrl"].ToString();
    if (string.IsNullOrEmpty(candidate)) candidate = query["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(candidate)) return "/";

    return candidate.StartsWith('/') && !candidate.StartsWith("//") ? candidate : "/";
}
