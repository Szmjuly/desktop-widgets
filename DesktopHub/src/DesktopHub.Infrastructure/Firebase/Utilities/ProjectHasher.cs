using System.Security.Cryptography;
using System.Text;
using DesktopHub.Infrastructure.Logging;

namespace DesktopHub.Infrastructure.Firebase.Utilities;

/// <summary>
/// HMAC-SHA256 hashing for project numbers → Firebase keys.
/// Uses a locally-stored secret so Firebase never sees plaintext project identifiers.
/// </summary>
public static class ProjectHasher
{
    private static readonly string SecretFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopHub", "tag_secret.key");

    private static byte[]? _cachedSecret;

    /// <summary>
    /// Compute the HMAC-SHA256 hash of a project number using the local secret.
    /// Returns a lowercase hex string suitable for use as a Firebase RTDB key.
    /// </summary>
    public static string HashProjectNumber(string projectNumber)
    {
        if (string.IsNullOrWhiteSpace(projectNumber))
            throw new ArgumentException("Project number cannot be empty.", nameof(projectNumber));

        var secret = GetOrCreateSecret();
        var normalized = NormalizeProjectNumber(projectNumber);

        using var hmac = new HMACSHA256(secret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Normalize project number for consistent hashing.
    /// Strips leading P prefix, trims whitespace, lowercases.
    /// "P250784.00" and "250784.00" hash to the same key.
    /// </summary>
    internal static string NormalizeProjectNumber(string projectNumber)
    {
        var num = projectNumber.Trim();

        // Strip leading 'P' prefix (Q-drive format)
        if (num.StartsWith("P", StringComparison.OrdinalIgnoreCase) && num.Length > 1 && char.IsDigit(num[1]))
            num = num[1..];

        return num.ToLowerInvariant();
    }

    /// <summary>
    /// Get or create the HMAC secret key. Generated once on first use, persisted locally.
    /// </summary>
    private static byte[] GetOrCreateSecret()
    {
        if (_cachedSecret != null)
            return _cachedSecret;

        if (File.Exists(SecretFilePath))
        {
            try
            {
                _cachedSecret = File.ReadAllBytes(SecretFilePath);
                if (_cachedSecret.Length >= 32)
                    return _cachedSecret;
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"ProjectHasher: Failed to read secret file: {ex.Message}");
            }
        }

        // Generate a new 256-bit secret
        _cachedSecret = RandomNumberGenerator.GetBytes(32);

        try
        {
            var dir = Path.GetDirectoryName(SecretFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(SecretFilePath, _cachedSecret);
            InfraLogger.Log($"ProjectHasher: Generated new secret at {SecretFilePath}");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"ProjectHasher: Failed to save secret file: {ex.Message}");
        }

        return _cachedSecret;
    }

    /// <summary>
    /// Export the secret as a Base64 string for sharing with side scripts.
    /// </summary>
    public static string ExportSecretBase64()
    {
        var secret = GetOrCreateSecret();
        return Convert.ToBase64String(secret);
    }

    /// <summary>
    /// Import a secret from a Base64 string (e.g. from a side script or another machine).
    /// Overwrites the existing secret file.
    /// </summary>
    public static void ImportSecretBase64(string base64Secret)
    {
        var secret = Convert.FromBase64String(base64Secret);
        if (secret.Length < 32)
            throw new ArgumentException("Secret must be at least 32 bytes.");

        _cachedSecret = secret;

        var dir = Path.GetDirectoryName(SecretFilePath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(SecretFilePath, secret);
        InfraLogger.Log("ProjectHasher: Imported secret from Base64");
    }
}
