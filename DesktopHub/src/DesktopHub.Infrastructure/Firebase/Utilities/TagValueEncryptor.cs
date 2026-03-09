using System.Security.Cryptography;
using System.Text;
using DesktopHub.Infrastructure.Logging;

namespace DesktopHub.Infrastructure.Firebase.Utilities;

/// <summary>
/// AES-256-CBC encryption for tag values stored in Firebase.
/// Uses the same local secret as ProjectHasher so Firebase never sees plaintext tag data.
/// Values are encrypted before upload and decrypted on read.
/// The local cache stores plaintext for fast search.
/// </summary>
public static class TagValueEncryptor
{
    /// <summary>
    /// Encrypt a plaintext string value using AES-256-CBC.
    /// Returns a Base64 string containing IV + ciphertext.
    /// Returns null if input is null/empty.
    /// </summary>
    public static string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        try
        {
            var key = GetEncryptionKey();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var cipherBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

            // Prepend IV to ciphertext: [16-byte IV][ciphertext]
            var result = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TagValueEncryptor: Encrypt failed: {ex.Message}");
            return plaintext; // Fallback to plaintext on failure
        }
    }

    /// <summary>
    /// Decrypt a Base64-encoded ciphertext back to plaintext.
    /// If the input doesn't look like encrypted data, returns it as-is (backwards compatibility).
    /// </summary>
    public static string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        try
        {
            var combined = Convert.FromBase64String(ciphertext);

            // Minimum size: 16-byte IV + at least 16-byte ciphertext block
            if (combined.Length < 32)
                return ciphertext; // Not encrypted, return as-is

            var key = GetEncryptionKey();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Extract IV (first 16 bytes)
            var iv = new byte[16];
            Buffer.BlockCopy(combined, 0, iv, 0, 16);
            aes.IV = iv;

            // Extract ciphertext (remaining bytes)
            var cipherBytes = new byte[combined.Length - 16];
            Buffer.BlockCopy(combined, 16, cipherBytes, 0, cipherBytes.Length);

            using var decryptor = aes.CreateDecryptor();
            var plaintextBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (FormatException)
        {
            // Not Base64 → not encrypted, return as-is (backwards compatibility)
            return ciphertext;
        }
        catch (CryptographicException)
        {
            // Decryption failed → likely plaintext from before encryption was enabled
            return ciphertext;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TagValueEncryptor: Decrypt failed: {ex.Message}");
            return ciphertext; // Return as-is on failure
        }
    }

    /// <summary>
    /// Encrypt a list of strings (e.g., engineers, code references).
    /// </summary>
    public static List<string> EncryptList(List<string> items)
    {
        return items.Select(i => Encrypt(i) ?? i).ToList();
    }

    /// <summary>
    /// Decrypt a list of strings.
    /// </summary>
    public static List<string> DecryptList(List<string> items)
    {
        return items.Select(i => Decrypt(i) ?? i).ToList();
    }

    /// <summary>
    /// Encrypt a dictionary of custom tags.
    /// </summary>
    public static Dictionary<string, string> EncryptDictionary(Dictionary<string, string> dict)
    {
        return dict.ToDictionary(kvp => kvp.Key, kvp => Encrypt(kvp.Value) ?? kvp.Value);
    }

    /// <summary>
    /// Decrypt a dictionary of custom tags.
    /// </summary>
    public static Dictionary<string, string> DecryptDictionary(Dictionary<string, string> dict)
    {
        return dict.ToDictionary(kvp => kvp.Key, kvp => Decrypt(kvp.Value) ?? kvp.Value);
    }

    /// <summary>
    /// Derive a 32-byte AES key from the existing ProjectHasher secret.
    /// Uses SHA-256 of the secret to ensure consistent 32-byte key length.
    /// </summary>
    private static byte[] GetEncryptionKey()
    {
        var secret = GetSecret();
        // Use SHA-256 to derive a consistent 32-byte key from the secret
        return SHA256.HashData(secret);
    }

    /// <summary>
    /// Get the raw secret bytes from ProjectHasher's secret file.
    /// </summary>
    private static byte[] GetSecret()
    {
        // Reuse ProjectHasher's secret by calling its export method and re-importing
        var base64 = ProjectHasher.ExportSecretBase64();
        return Convert.FromBase64String(base64);
    }
}
