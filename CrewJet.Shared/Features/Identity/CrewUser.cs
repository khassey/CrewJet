namespace CrewJet.Shared.Features.Identity;

public class CrewUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;

    // Identity linking
    public string SubjectId { get; set; } = string.Empty; // OpenIddict's unique user ID
    public string ExternalId { get; set; } = string.Empty; // From external IdP (M365 ObjectId, etc.)
    public string AuthProvider { get; set; } = "Local";

    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; } = false;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public string? Phone { get; set; }
    public List<string> Roles { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? PasswordHash { get; set; }
}
