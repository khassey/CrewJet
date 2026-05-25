using CrewJet.Server.Features.Identity.Models;
using Marten;

namespace CrewJet.Server.Features.Identity.Persistence;

/// <summary>
/// Marten-backed <see cref="ICrewUserStore"/>. The injected <see cref="IDocumentSession"/>
/// is already tenant-scoped (see <c>CrewJetDocumentStore</c>), so queries filter on the
/// current tenant automatically. The <c>tenantId</c> parameter is validated against the
/// session's tenant as a defensive guard.
/// </summary>
public sealed class MartenCrewUserStore(IDocumentSession session) : ICrewUserStore
{
    public async Task<CrewUser?> FindByTenantAndEmailAsync(string tenantId, string email, CancellationToken ct = default)
    {
        AssertTenantMatch(tenantId);

        return await session.Query<CrewUser>()
            .Where(u => u.Email == email)
            .FirstOrDefaultAsync(ct);
    }

    public Task<CrewUser?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => session.LoadAsync<CrewUser>(id, ct);

    public async Task<CrewUser?> FindByExternalIdAsync(string externalId, CancellationToken ct = default)
    {
        return await session.Query<CrewUser>()
            .Where(u => u.ExternalId == externalId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveAsync(CrewUser user, CancellationToken ct = default)
    {
        AssertTenantMatch(user.TenantId);

        session.Store(user);
        await session.SaveChangesAsync(ct);
    }

    private void AssertTenantMatch(string tenantId)
    {
        if (!string.Equals(session.TenantId, tenantId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Tenant mismatch: session is scoped to '{session.TenantId}' but operation requested '{tenantId}'.");
    }
}
