using System.Security.Cryptography;
using System.Text;

namespace CYHDocG.Repo.Security;

/// <summary>
/// Provides secure encryption and decryption for query parameters and resource IDs to prevent URL tampering.
/// </summary>
public static class UrlProtector
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("CYHDocGuru#Secret2026MasterKey!@"); // 32 bytes for AES-256
    private static readonly byte[] Iv = Encoding.UTF8.GetBytes("CYH_IV_Init_2026"); // 16 bytes IV

    public static string EncryptId(long id)
    {
        return Encrypt(id.ToString());
    }

    public static bool TryDecryptId(string? cipherText, out long id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(cipherText)) return false;

        try
        {
            var plain = Decrypt(cipherText);
            return long.TryParse(plain, out id);
        }
        catch
        {
            return false;
        }
    }

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = Iv;

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // URL-Safe Base64
        return Convert.ToBase64String(cipherBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        // Restore URL-safe Base64
        string base64 = cipherText.Replace("-", "+").Replace("_", "/");
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var cipherBytes = Convert.FromBase64String(base64);

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = Iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
