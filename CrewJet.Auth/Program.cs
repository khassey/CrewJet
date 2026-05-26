using CrewJet.Auth.Components;
using CrewJet.Auth.Data;
using CrewJet.Auth.Features.Identity;
using CrewJet.Auth.Features.Identity.Seeding;
using CrewJet.Auth.Features.Identity.Services;
using CrewJet.Shared.Features.Identity;
using CrewJet.Shared.Features.Identity.TenantResolution;
using JasperFx;
using JasperFx.MultiTenancy;
using Marten;
using Marten.Schema;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// =============================================
// Blazor + Core Services
// =============================================
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// Cookie authentication for the Auth server's own UI pages (/login, /register, /account/*).
// OpenIddict handles its own schemes for the protocol endpoints (/connect/*).
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/connect/logout";
        options.Cookie.Name = "CrewJet.Auth";
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Domain = builder.Environment.IsDevelopment() ? ".crewjet.test" : ".crewjet.io";
    });

builder.Services.AddTenantResolution(builder.Configuration);

// Password hasher is now keyed on the platform-wide directory entry (one credential per user).
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// =============================================
// Persistence
// =============================================
// Marten for UserProfile (tenanted) + User / Tenant (platform-wide).
var connection = builder.Configuration.GetConnectionString("CrewJetDatabase")
                 ?? throw new InvalidOperationException("Connection string 'CrewJetDatabase' not found.");

builder.Services.AddMarten(opts =>
{
    opts.Connection(connection);

    // Every document gets a tenant_id column and queries are tenant-filtered automatically.
    opts.UseSystemTextJsonForSerialization();
    opts.AutoCreateSchemaObjects = builder.Environment.IsDevelopment() ? AutoCreate.All : AutoCreate.None;

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
        .SingleTenanted()
        .UniqueIndex(UniqueIndexType.Computed, x => x.Email);

    opts.Schema.For<Tenant>()
        .Identity(x => x.Id)
        .SingleTenanted()
        .UniqueIndex(UniqueIndexType.Computed, x => x.Slug)
        .UniqueIndex(UniqueIndexType.Computed, x => x.Name);

    // Tenanted document — Identity config still required for the typed Id.
    opts.Schema.For<UserProfile>()
        .Identity(x => x.Id);
});

// No scoped IDocumentSession registration. Auth is platform-level UI —
// every consumer (RegistrationService, UserDirectory, LoginService) opens its
// own session explicitly via IDocumentStore with the tenancy semantics it
// actually wants. This avoids a tripwire where an anonymous Auth page could
// accidentally pull a session bound to whatever the URL host parsed to.

builder.Services.AddDbContext<OpenIddictDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("OpenIddictDatabase")
        ?? throw new InvalidOperationException("Connection string 'OpenIddictDatabase' not found."));

    options.UseOpenIddict();
});

// =============================================
// OpenIddict Authorization Server
// =============================================
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<OpenIddictDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetUserInfoEndpointUris("/connect/userinfo")
            .SetEndSessionEndpointUris("/connect/logout");

        // Enable flows
        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow();

        options.RequireProofKeyForCodeExchange(); // PKCE mandatory

        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile,
            "crewjet:tenant" // Custom scope for tenant context
        );

        // Signing & Encryption (Dev)
        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .DisableTransportSecurityRequirement(); // For local dev
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// =============================================
// Identity Services
// =============================================
builder.Services.AddScoped<IUserDirectory, UserDirectory>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<TenantClientRegistrar>();

// Seed OpenIddict clients (crewjet-server BFF) on startup
builder.Services.AddHostedService<OpenIddictSeeder>();

var app = builder.Build();

// -----------------------------------------------------------------------------
// Development-only: auto-apply EF Core migrations for OpenIddictDbContext.
// Marten schemas auto-create via AutoCreate.All in dev.
// -----------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<OpenIddictDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error/{0}", createScopeForStatusCodePages: true);

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseTenantResolution();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapOidcEndpoints();

app.Run();
