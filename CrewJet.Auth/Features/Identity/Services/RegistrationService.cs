using CrewJet.Shared.Features.Identity;
using Microsoft.AspNetCore.Identity;

namespace CrewJet.Auth.Features.Identity.Services;

public class RegistrationService(
    CrewUserManager userManager,
    IPasswordHasher<CrewUser> passwordHasher)
{
    public async Task<(bool Success, string Message)> RegisterLocalUserAsync(
        string email, string firstName, string lastName, string password)
    {
        email = email.ToLowerInvariant().Trim();

        if (await userManager.FindByEmailAsync(email) != null)
            return (false, "A user with this email already exists.");

        var crewUser = new CrewUser
        {
            SubjectId = Guid.NewGuid().ToString(),
            Email = email,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            DisplayName = $"{firstName.Trim()} {lastName.Trim()}".Trim(),
            AuthProvider = "Local"
        };

        crewUser.PasswordHash = passwordHasher.HashPassword(crewUser, password);

        await userManager.CreateAsync(crewUser);

        return (true, "Account created successfully!");
    }
}
