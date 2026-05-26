using CrewJet.Shared.Features.Identity;
using Microsoft.AspNetCore.Identity;

namespace CrewJet.Auth.Features.Identity.Services;

public sealed record LoginResult(bool Success, string? Error, User? Entry);

/// <summary>
/// Cross-tenant credential verification. Reads the platform-wide
/// <see cref="User"/> by email and verifies the supplied password
/// against the stored hash. Tenant attachment is intentionally NOT part of this
/// step — the caller decides where to send the user post-login.
/// </summary>
public sealed class LoginService(
    IUserDirectory directory,
    IPasswordHasher<User> passwordHasher)
{
    public async Task<LoginResult> VerifyCredentialsAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return new LoginResult(false, "Email and password are required.", null);

        var entry = await directory.FindByEmailAsync(email);

        if (entry is null || string.IsNullOrEmpty(entry.PasswordHash))
            return new LoginResult(false, "Invalid email or password.", null);

        var result = passwordHasher.VerifyHashedPassword(entry, entry.PasswordHash, password);

        if (result is PasswordVerificationResult.Failed)
            return new LoginResult(false, "Invalid email or password.", null);

        if (entry.Tenants.Count == 0)
            return new LoginResult(false, "Your account is not attached to any workspace. Please register a company.", null);

        return new LoginResult(true, null, entry);
    }
}
