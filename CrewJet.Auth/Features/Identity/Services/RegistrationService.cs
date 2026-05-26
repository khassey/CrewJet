using CrewJet.Shared.Features.Identity;
using Marten;
using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace CrewJet.Auth.Features.Identity.Services;

public sealed record RegisterNewCompanyResult(
    bool Success,
    string Message,
    string? Slug);

/// <summary>
/// Handles the "new company + first user" bootstrap on the Auth server.
///
/// All three persistence writes — <see cref="User"/> (non-tenanted),
/// <see cref="UserProfile"/> (tenanted), and <see cref="Tenant"/> (non-tenanted) —
/// are made on a single Marten session and committed in one SaveChangesAsync.
/// Marten's policy still flags everything as multi-tenanted by default, with
/// the two registry documents overridden as SingleTenanted; Load and Store on
/// those types ignore the session's tenant context, while <see cref="UserProfile"/> writes
/// inherit it. Net effect: a single Postgres transaction wraps the lot, so a
/// crash before SaveChangesAsync rolls everything back — no orphan directory
/// entries, no half-created tenants.
///
/// OpenIddict redirect-URI registration is intentionally outside the
/// transaction: it touches a separate database (the OpenIddict EF context)
/// and is idempotent + re-runnable from the seeder, so a partial failure is
/// recoverable on the next start.
/// </summary>
public class RegistrationService(
    IPasswordHasher<User> passwordHasher,
    IDocumentStore documentStore,
    TenantClientRegistrar clientRegistrar)
{
    public async Task<RegisterNewCompanyResult> RegisterNewCompanyAsync(
        string? email,
        string? firstName,
        string? lastName,
        string? password,
        string? companyName,
        string? desiredSubdomain)
    {
        email = (email ?? string.Empty).ToLowerInvariant().Trim();
        companyName = (companyName ?? string.Empty).Trim();
        firstName = (firstName ?? string.Empty).Trim();
        lastName = (lastName ?? string.Empty).Trim();

        var subdomain = SanitizeToSubdomain(desiredSubdomain);

        if (string.IsNullOrWhiteSpace(email))
            return new RegisterNewCompanyResult(false, "Email is required.", null);

        if (string.IsNullOrWhiteSpace(password))
            return new RegisterNewCompanyResult(false, "Password is required.", null);

        if (string.IsNullOrWhiteSpace(companyName))
            return new RegisterNewCompanyResult(false, "Company name is required.", null);

        if (string.IsNullOrWhiteSpace(subdomain))
            return new RegisterNewCompanyResult(false, "Company subdomain is required and must contain valid characters.", null);

        if (await UserExists(email))
            return new RegisterNewCompanyResult(false, "That email is already taken.", null);

        if (await TenantExists(subdomain))
            return new RegisterNewCompanyResult(false, "That company subdomain is already taken.", null);

        var tenantId = TenantId.New();
        await using var session = documentStore.LightweightSession(tenantId.ToString());

        var tenantDoc = new Tenant
        {
            Id = tenantId,
            Name = companyName,
            Slug = subdomain
        };

        var userDoc = new User
        {
            Id = UserId.New(),
            Email = email,
            SubjectId = Guid.NewGuid().ToString(),
            FirstName = firstName,
            LastName = lastName,
            Tenants = [tenantId]
        };

        userDoc.PasswordHash = passwordHasher.HashPassword(userDoc, password);

        // SubjectId is the OIDC 'sub' claim — one stable value per user across
        // every workspace they belong to. UserProfile mirrors User.SubjectId.
        var userProfileDoc = new UserProfile
        {
            Id = UserProfileId.New(),
            TenantId = tenantId,
            UserId = userDoc.Id,
            SubjectId = userDoc.SubjectId,
            DisplayName = $"{firstName} {lastName}".Trim(),
            AuthProvider = "Local",
            Roles = ["Admin"],
            CreatedBy = userDoc.Id.ToGuid().ToString()
        };

        session.Store(tenantDoc);
        session.Store(userDoc);
        session.Store(userProfileDoc);

        await session.SaveChangesAsync();

        // Register this tenant's subdomain redirect URIs with the OpenIddict
        // client, so the immediate OIDC handshake from the new subdomain succeeds.
        // Lives outside the Marten transaction on purpose (different DB).
        await clientRegistrar.EnsureRedirectsForTenantAsync(subdomain);

        return new RegisterNewCompanyResult(true, "Company account created successfully!", Slug: subdomain);
    }

    private async Task<bool> TenantExists(string subdomain)
    {
        // --- Existence check on the Tenant (non-tenanted) ---
        await using var session = documentStore.LightweightSession();
        return await session.Query<Tenant>().AnyAsync(t => t.Slug == subdomain);
    }

    private async Task<bool> UserExists(string email)
    {
        // --- Existence check on the user (non-tenanted) ---
        await using var session = documentStore.LightweightSession();
        return await session.Query<User>().AnyAsync(t => t.Email == email);
    }

    // Very small, pragmatic slug sanitizer. Production version should be more thorough.
    private static string SanitizeToSubdomain(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var lower = input.Trim().ToLowerInvariant();

        var chars = lower.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var candidate = new string(chars);

        candidate = Regex.Replace(candidate, "-+", "-").Trim('-');

        if (candidate.Length > 63)
            candidate = candidate[..63].TrimEnd('-');

        return candidate;
    }
}
