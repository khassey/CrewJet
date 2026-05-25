using CrewJet.Server.Components;
using CrewJet.Server.Data;
using CrewJet.Server.Features.Identity.Claims;
using CrewJet.Server.Features.Identity.Persistence;
using CrewJet.Server.Features.Identity.Services;
using CrewJet.Server.Features.Identity.TenantResolution;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// === Core foundations ===
builder.Services.AddHttpContextAccessor();
builder.Services.AddTenantResolution(builder.Configuration);
builder.Services.AddCrewJetMarten(builder.Configuration, builder.Environment);

// TODO: Remove these after cleanup is complete (old hybrid identity code)
builder.Services.AddScoped<ICrewUserStore, MartenCrewUserStore>();
builder.Services.AddScoped<UserLinkingService>();
builder.Services.AddTransient<IClaimsTransformation, CrewUserClaimsTransformer>();

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.UseTenantResolution(); // Keep - very important

app.UseRouting();
app.UseAuthentication(); // Keep for now (will be reconfigured)
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(CrewJet.Client._Imports).Assembly);

app.Run();
