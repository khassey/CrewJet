using CrewJet.Server.Features.Identity.Models;

namespace CrewJet.Server.Features.Identity.Persistence;

// Temporary persistence abstraction. Will be backed by Marten once the DocumentStore
// is configured. Kept narrow on purpose so the in-memory impl stays trivial.
public interface ICrewUserStore
{
    Task<CrewUser?> FindByTenantAndEmailAsync(string tenantId, string email, CancellationToken ct = default);

    Task<CrewUser?> FindByIdAsync(Guid id, CancellationToken ct = default);

    Task<CrewUser?> FindByExternalIdAsync(string externalId, CancellationToken ct = default);

    Task SaveAsync(CrewUser user, CancellationToken ct = default);
}
