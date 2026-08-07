using System.Security.Cryptography;

namespace VSP.Core.Security;

/// <summary>
/// PBKDF2-HMACSHA256 password hashing via the .NET BCL (<see cref="Rfc2898DeriveBytes"/>) --
/// no external package, no custom cryptography. <see cref="DefaultIterations"/> is only the
/// value used at hash time; Verify always uses the iteration count passed in (persisted per-row
/// by the caller), so a future increase to DefaultIterations never invalidates already-hashed
/// passwords.
/// </summary>
public static class PasswordHasher
{
    public const int DefaultIterations = 210_000;

    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public static (string Hash, string Salt, int Iterations) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, HashSizeBytes);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt), DefaultIterations);
    }

    public static bool Verify(string password, string hash, string salt, int iterations)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expectedHash = Convert.FromBase64String(hash);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
