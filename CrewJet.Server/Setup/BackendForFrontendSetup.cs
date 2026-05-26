using CrewJet.Server.Features.Identity;
using CrewJet.Shared.Features.Identity;
using CrewJet.Shared.Features.Identity.TenantResolution;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;

namespace CrewJet.Server.Setup;

public static class BackendForFrontendSetup
{
    public static IServiceCollection AddBffAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services
            .AddTenantResolution(configuration)

            // Register the transformation that turns the "tenant_id" claim (minted in OnTicketReceived)
            // into the authoritative value on the scoped ITenantContext for every authenticated request.
            // This must live in Server because only Server owns the final application cookie.
            .AddTransient<IClaimsTransformation, AuthenticatedTenantClaimsTransformation>()

            // ===============================================================================
            // Authentication: Cookie (application session) + OpenIdConnect (to CrewJet.Auth)
            // BFF pattern: Server receives the OIDC ticket and mints the authoritative
            // application cookie containing the "tenant_id" claim. The claim is the required
            // source for ITenantContext / data layer isolation.
            // ===============================================================================
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/access-denied";
                options.Cookie.Name = "CrewJet.Session";
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.Domain = environment.IsDevelopment() ? ".crewjet.test" : ".crewjet.io";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                var authSection = configuration.GetSection("Auth");

                options.Authority = authSection["Authority"]
                                    ?? throw new InvalidOperationException("Auth:Authority is required (the CrewJet.Auth base URL).");

                options.ClientId = authSection["ClientId"] ?? "crewjet-server";
                options.ClientSecret = authSection["ClientSecret"]
                                       ?? throw new InvalidOperationException("Auth:ClientSecret is required for the confidential client.");

                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;

                options.Scope.Clear();
                options.Scope.Add(OpenIdConnectScope.OpenId);
                options.Scope.Add(OpenIdConnectScope.Profile);
                options.Scope.Add(OpenIdConnectScope.Email);
                options.Scope.Add("crewjet:tenant");

                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;

                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

                // Pull tenant memberships from userinfo into the principal as repeated claims.
                // The Auth side emits these from the User; Server uses them for the
                // future "active tenant selector" when a user belongs to more than one workspace.
                options.ClaimActions.MapJsonKey(
                    TenantResolutionOptions.TenantMembershipClaimType,
                    TenantResolutionOptions.TenantMembershipClaimType);

                options.CallbackPath = "/signin-oidc";
                options.SignedOutCallbackPath = "/signout-callback-oidc";
                options.RemoteSignOutPath = "/signout-oidc";

                // ================================================================================
                // Claim enrichment after OIDC ticket
                // ================================================================================
                // The initiation subdomain (captured at challenge time) is the authoritative
                // active tenant. We stash it across the OIDC round-trip and apply it to the
                // principal as the "tenant_id" claim before the Cookie handler mints the
                // application session cookie.
                // ================================================================================
                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = ctx =>
                    {
                        // The initiation tenant is the subdomain the user started on —
                        // a pre-authentication hint by definition. We stash it across the
                        // OIDC round-trip so OnTicketReceived can mint the secure claim.
                        var hint = ctx.HttpContext.RequestServices.GetRequiredService<ITenantHint>();

                        if (hint.HasTenantSubdomain && !string.IsNullOrWhiteSpace(hint.TenantSubdomain))
                            ctx.Properties.Items["initiation_tenant"] = hint.TenantSubdomain;

                        return Task.CompletedTask;
                    },

                    OnTicketReceived = async ctx =>
                    {
                        var hint = ctx.HttpContext.RequestServices.GetRequiredService<ITenantHint>();
                        var resolutionOptions = ctx.HttpContext.RequestServices.GetRequiredService<IOptions<TenantResolutionOptions>>().Value;
                        var claimType = resolutionOptions.TenantClaimType;

                        var documentStore = ctx.HttpContext.RequestServices.GetRequiredService<IDocumentStore>();

                        var subdomain = ctx.Properties?.Items.TryGetValue("initiation_tenant", out var stashed) is true && !string.IsNullOrWhiteSpace(stashed)
                            ? stashed
                            : hint.TenantSubdomain;

                        if (string.IsNullOrWhiteSpace(subdomain)) return;

                        await using var session = documentStore.LightweightSession();

                        var tenant = await session.Query<Tenant>()
                            .Where(t => t.Slug == subdomain)
                            .FirstOrDefaultAsync();

                        if (tenant is null) return;

                        var identity = (ClaimsIdentity?)ctx.Principal?.Identity;
                        if (identity is null) return;

                        var value = tenant.Id.ToString();

                        if (!identity.HasClaim(c => c.Type == claimType && c.Value == value))
                            identity.AddClaim(new Claim(claimType, value));
                    }
                };
            });

        return services;
    }
}
