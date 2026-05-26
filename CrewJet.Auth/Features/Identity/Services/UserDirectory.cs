using CrewJet.Shared.Features.Identity;
using Marten;

namespace CrewJet.Auth.Features.Identity.Services;

/// <inheritdoc />
public sealed class UserDirectory(IDocumentStore store) : IUserDirectory
{
    public async Task<User?> FindByEmailAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalizedEmail = Normalize(email);

        await using var session = store.QuerySession();

        return await session.Query<User>()
            .Where(x => x.Email == normalizedEmail)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(User user)
    {
        user.Email = Normalize(user.Email);

        await using var session = store.LightweightSession();

        var existing = await session.Query<User>()
            .Where(x => x.Email == user.Email)
            .FirstOrDefaultAsync();

        if (existing is not null)
            throw new InvalidOperationException($"A user already exists for '{user.Email}'.");

        session.Store(user);
        await session.SaveChangesAsync();
    }

    public async Task AddTenantAsync(string email, TenantId tenantId)
    {
        var normalizedEmail = Normalize(email);

        await using var session = store.LightweightSession();

        var user = await session.Query<User>()
                       .Where(x => x.Email == normalizedEmail)
                       .FirstOrDefaultAsync()
                   ?? throw new InvalidOperationException($"User not found for for '{email}'.");

        if (user.Tenants.Contains(tenantId))
            return;

        user.Tenants.Add(tenantId);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        session.Store(user);
        await session.SaveChangesAsync();
    }

    private static string Normalize(string email) =>
        email.Trim().ToLowerInvariant();
}
