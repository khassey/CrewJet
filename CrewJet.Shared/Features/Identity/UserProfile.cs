namespace CrewJet.Shared.Features.Identity;

public readonly record struct UserProfileId(Guid Value)
{
    public Guid ToGuid() => Value;

    public static UserProfileId New() => new(Guid.NewGuid());
    public static UserProfileId From(Guid id) => new(id);
}

/// <summary>
/// Per tenant user profile
/// </summary>
public record UserProfile
{
    public UserProfileId Id { get; init; } = UserProfileId.New();
    public TenantId TenantId { get; init; } = TenantId.New();

    // Identity linking
    public UserId UserId { get; set; } = UserId.New();
    public string SubjectId { get; set; } = string.Empty; // From IdP (Local, M365 ObjectId, etc.)
    public string AuthProvider { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
