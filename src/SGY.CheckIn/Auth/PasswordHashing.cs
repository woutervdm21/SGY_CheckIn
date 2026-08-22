using Microsoft.AspNetCore.Identity;

namespace SGY.CheckIn.Auth;

/// <summary>
/// Wraps ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/> to hash/verify the
/// single shared staff password, without pulling in the rest of Identity's user-store
/// machinery (there are no per-user accounts in this app).
/// </summary>
public static class PasswordHashing
{
    private static readonly PasswordHasher<object> Hasher = new();

    public static string Hash(string password) => Hasher.HashPassword(default!, password);

    public static bool Verify(string? storedHash, string suppliedPassword)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(suppliedPassword))
        {
            return false;
        }

        var result = Hasher.VerifyHashedPassword(default!, storedHash, suppliedPassword);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
