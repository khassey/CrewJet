using CrewJet.Auth.Components;
using CrewJet.Auth.Data;
using CrewJet.Auth.Features.Identity.Services;
using CrewJet.Shared.Features.Identity.TenantResolution;
using JasperFx;
using Marten;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();

builder.Services.AddTenantResolution(builder.Configuration);

// =============================================
// Marten
// =============================================
//builder.Services.AddCrewJetMarten(builder.Configuration, builder.Environment);

// Temporary Marten setup for Auth project
var connection = builder.Configuration.GetConnectionString("CrewJetDatabase")
                 ?? throw new InvalidOperationException(
                     $"Connection string 'CrewJetDatabase' not found. Add it to appsettings or user secrets.");

builder.Services.AddMarten(opts =>
{
    opts.Connection(connection);

    // Every document gets a tenant_id column and queries are tenant-filtered automatically.
    opts.Policies.AllDocumentsAreMultiTenanted();

    // System.Text.Json keeps the dependency footprint small; switch to Newtonsoft if we
    // later need polymorphism or other features it provides.
    opts.UseSystemTextJsonForSerialization();

    // Auto-create schema/tables in dev. In prod we'll use explicit migrations.
    opts.AutoCreateSchemaObjects = builder.Environment.IsDevelopment() ? AutoCreate.All : AutoCreate.None;
});

// Tenant-scoped session per request. Resolving IDocumentSession outside an HTTP request
// scope (background workers, etc.) will throw — fine for now, revisit when we add them.
builder.Services.AddScoped<IDocumentSession>(sp =>
{
    var store = sp.GetRequiredService<IDocumentStore>();
    var tenantContext = sp.GetRequiredService<ITenantContext>();
    return store.LightweightSession(tenantContext.TenantId);
});

// =============================================
// OpenIddict
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

        options.RegisterScopes("openid", "profile", "email", "offline_access");

        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .AllowPasswordFlow()           // For local username/password login
            .RequireProofKeyForCodeExchange();

        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        options.DisableAccessTokenEncryption();
    })
    .AddClient(options =>
    {
        options.AllowPasswordFlow();
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// OpenIddict EF Core store
builder.Services.AddDbContext<OpenIddictDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("CrewJetAuth"));
    options.UseOpenIddict();
});

builder.Services.AddScoped<CrewUserManager>();
builder.Services.AddScoped<RegistrationService>();


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

        options.RegisterScopes("openid", "profile", "email", "offline_access");

        options.AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .AllowPasswordFlow()
            .RequireProofKeyForCodeExchange();

        // Development certificates
        options.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        // Allow client apps to use it
        options.DisableAccessTokenEncryption();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// Temporary EF Core store for OpenIddict (until we migrate to Marten)
builder.Services.AddDbContext<OpenIddictDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("CrewJetAuth"));
    options.UseOpenIddict();
});

var app = builder.Build();

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

app.Run();
