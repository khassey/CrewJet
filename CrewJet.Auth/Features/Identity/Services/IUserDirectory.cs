using CrewJet.Shared.Features.Identity;

namespace CrewJet.Auth.Features.Identity.Services;

/// <summary>
/// Non-tenanted lookup over the platform-wide <see cref="User"/> store.
/// Used by the login page (cross-tenant credential lookup) and the registration
/// flow (single-write directory creation + tenant attachment).
/// </summary>
public interface IUserDirectory
{
    Task<User?> FindByEmailAsync(string? email);

    /// <summary>
    /// Creates a brand-new directory entry. Throws if an entry already exists for the email.
    /// </summary>
    Task CreateAsync(User user);

    /// <summary>
    /// Appends a tenant membership to an existing entry. No-op if already present.
    /// </summary>
    Task AddTenantAsync(string email, TenantId tenantId);
}
