namespace CrewJet.Shared.Features.Identity;

public record CrewUserDto(
    Guid Id,
    string TenantId,
    string Email,
    string FirstName,
    string LastName,
    string DisplayName,
    IdentityProvider AuthProvider,
    bool IsActive,
    string? EmployeeNumber,
    string? Phone,
    string? JobTitle,
    IReadOnlyList<string> Roles);
