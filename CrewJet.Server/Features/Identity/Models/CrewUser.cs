namespace CrewJet.Server.Features.Identity.Models;

public class CrewUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty; // From IdP (Microsoft, Google, etc.)
    public string AuthProvider { get; set; } = "Local"; // "Local", "Microsoft365", "Google", etc.

    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool EmailConfirmed { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public string? Phone { get; set; }
    public string? EmployeeNumber { get; set; }
    public List<string> Roles { get; set; } = new();
}
