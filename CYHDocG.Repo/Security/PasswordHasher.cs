using System.Security.Cryptography;
using System.Text;

namespace CYHDocG.Repo.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing with transparent legacy plaintext migration support.
/// </summary>
public static class PasswordHasher
{
    private const string Prefix = "PBKDF2$v1$";
    private const int DefaultIterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Prefix}{DefaultIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? storedHash, out bool needsRehash)
    {
        needsRehash = false;
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        if (storedHash.StartsWith(Prefix, StringComparison.Ordinal))
        {
            string[] parts = storedHash.Split('$');
            if (parts.Length != 5 || !int.TryParse(parts[2], out int iterations) || iterations <= 0)
            {
                return false;
            }

            try
            {
                byte[] salt = Convert.FromBase64String(parts[3]);
                byte[] expectedHash = Convert.FromBase64String(parts[4]);
                byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

                bool matched = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
                needsRehash = matched && iterations < DefaultIterations;
                return matched;
            }
            catch
            {
                return false;
            }
        }

        // Legacy plaintext fallback for pre-existing DB accounts; triggers immediate upgrade on login
        byte[] inputDigest = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        byte[] storedDigest = SHA256.HashData(Encoding.UTF8.GetBytes(storedHash));
        bool legacyMatched = CryptographicOperations.FixedTimeEquals(inputDigest, storedDigest);
        needsRehash = legacyMatched;
        return legacyMatched;
    }
}
