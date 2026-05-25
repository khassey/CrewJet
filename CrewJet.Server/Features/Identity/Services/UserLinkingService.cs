using CrewJet.Server.Data;
using CrewJet.Server.Features.Identity.Models;
using CrewJet.Server.Features.Identity.Persistence;
using CrewJet.Server.Features.Identity.TenantResolution;
using CrewJet.Shared.Features.Identity;

namespace CrewJet.Server.Features.Identity.Services;

public sealed class UserLinkingService(
    ICrewUserStore store,
    ITenantContext tenantContext,
    ILogger<UserLinkingService> logger)
{
    public async Task<CrewUser> GetOrCreateCrewUserAsync(
        ApplicationUser identityUser,
        CancellationToken ct = default)
    {
        var tenantId = tenantContext.TenantId;
        var email = identityUser.Email ?? identityUser.UserName ?? string.Empty;

        var existing = await store.FindByTenantAndEmailAsync(tenantId, email, ct);

        if (existing is not null)
        {
            existing.LastLoginAt = DateTimeOffset.UtcNow;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            await store.SaveAsync(existing, ct);
            return existing;
        }

        var now = DateTimeOffset.UtcNow;

        var crewUser = new CrewUser
        {
            TenantId = tenantId,
            ExternalId = identityUser.Id,
            AuthProvider = UserProvider.Local,
            Email = email,
            DisplayName = email, // best effort until profile fields exist on ApplicationUser
            CreatedAt = now,
            LastLoginAt = now,
            CreatedBy = "system" // no acting principal during self-registration
        };

        await store.SaveAsync(crewUser, ct);

        logger.LogInformation("Created CrewUser {CrewUserId} for ApplicationUser {IdentityId} in tenant {TenantId}",
            crewUser.Id, identityUser.Id, tenantId);

        return crewUser;
    }
}
