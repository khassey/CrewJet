namespace CrewJet.Shared.Features.Identity;

/// <summary>
/// Strongly typed user identifier.
/// </summary>
/// <param name="Value"></param>
public readonly record struct UserId(Guid Value)
{
    public Guid ToGuid() => Value;

    public static UserId New() => new(Guid.NewGuid());
    public static UserId From(Guid id) => new(id);
}

/// <summary>
/// Platform-wide credential and identity directory record. Keyed by email.
/// Lives outside any tenant partition (registered as SingleTenanted in Marten)
/// so the login page can resolve a user before a tenant context exists.
///
/// One entry per email. The <see cref="Tenants"/> list is the set of workspaces
/// this user belongs to. New tenants are appended via the registration / invite
/// flows. The tenant-scoped <see cref="UserProfile"/> document holds per-workspace
/// profile data (display name, roles) and references this entry by SubjectId.
/// </summary>
public record User
{
    /// <summary>
    /// Marten document Id.
    /// </summary>
    public UserId Id { get; init; } = UserId.New();

    /// <summary>
    /// Stable identity used as the OIDC <c>sub</c> claim across every tenant
    /// this user belongs to.
    /// </summary>
    public string SubjectId { get; init; } = string.Empty;

    /// <summary>
    /// Hashed password for local-IdP authentication. Null when the user only
    /// authenticates through a federated upstream (M365, etc.).
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// User deets
    /// </summary>
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; } = false;

    /// <summary>
    /// TenantIds the user is a member of. Order is not significant.
    /// </summary>
    public List<TenantId> Tenants { get; set; } = new();

    /// Create/update deets
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
