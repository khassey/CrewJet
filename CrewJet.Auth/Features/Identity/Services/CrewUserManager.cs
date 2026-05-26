using CrewJet.Shared.Features.Identity;
using CrewJet.Shared.Features.Identity.TenantResolution;
using Marten;

namespace CrewJet.Auth.Features.Identity.Services;

public class CrewUserManager(IDocumentSession session, ITenantContext tenantContext)
{
    private string CurrentTenantId => tenantContext.TenantId; // This will throw if not resolved

    public async Task<CrewUser?> FindByEmailAsync(string email) =>
        await session.Query<CrewUser>()
            .Where(u => u.TenantId == CurrentTenantId && u.Email == email.ToLowerInvariant())
            .FirstOrDefaultAsync();

    public async Task<CrewUser?> FindBySubjectIdAsync(string subjectId) =>
        await session.Query<CrewUser>()
            .Where(u => u.SubjectId == subjectId && u.TenantId == CurrentTenantId)
            .FirstOrDefaultAsync();

    public async Task CreateAsync(CrewUser user)
    {
        user.TenantId = CurrentTenantId;

        if (string.IsNullOrEmpty(user.SubjectId))
            user.SubjectId = Guid.NewGuid().ToString();

        session.Store(user);
        await session.SaveChangesAsync();
    }

    public async Task UpdateAsync(CrewUser user)
    {
        if (user.TenantId != CurrentTenantId)
            throw new InvalidOperationException("Cannot update user from a different tenant");

        session.Update(user);
        await session.SaveChangesAsync();
    }
}
