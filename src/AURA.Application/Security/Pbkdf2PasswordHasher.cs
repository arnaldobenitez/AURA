using System.Security.Cryptography;

namespace AURA.Application.Security;

/// <summary>
/// Shared PBKDF2-HMAC-SHA256 hashing used by both AgentCredential secrets and human login
/// passwords - same algorithm/iteration count, one place to change it. Never stores or compares
/// plaintext; comparisons are constant-time.
/// </summary>
public static class Pbkdf2PasswordHasher
{
    public const int SaltByteLength = 16;
    public const int HashByteLength = 32;
    // OWASP-recommended minimum for PBKDF2-HMAC-SHA256 as of the 2023 cheat sheet revision.
    public const int Iterations = 210_000;

    public static (byte[] Salt, byte[] Hash) Hash(byte[] secretBytes)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltByteLength);
        var hash = Rfc2898DeriveBytes.Pbkdf2(secretBytes, salt, Iterations, HashAlgorithmName.SHA256, HashByteLength);
        return (salt, hash);
    }

    public static bool Verify(byte[] suppliedSecretBytes, byte[] salt, byte[] expectedHash, int iterations)
    {
        var suppliedHash = Rfc2898DeriveBytes.Pbkdf2(suppliedSecretBytes, salt, iterations, HashAlgorithmName.SHA256, HashByteLength);
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }
}
