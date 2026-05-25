using CrewJet.Shared.Features.Identity;

namespace CrewJet.Server.Features.Identity.Models;

public class CrewUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string TenantId { get; set; } = string.Empty; // Critical for multi-tenancy

    public string ExternalId { get; set; } = string.Empty; // Entra ID ObjectId, etc.
    public UserProvider AuthProvider { get; set; } = UserProvider.Local;

    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    // CrewJet-specific fields
    public string? EmployeeNumber { get; set; }
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }

    public List<string> Roles { get; set; } = new(); // e.g. "Technician", "CrewLead", "Manager"

    // Audit
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset? UpdatedAt { get; set; }
}
