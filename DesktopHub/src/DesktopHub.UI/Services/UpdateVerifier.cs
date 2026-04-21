using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace DesktopHub.UI.Services;

/// <summary>
/// Verifies that a downloaded update binary was signed by a trusted release key.
///
/// Public keys (one or more) are embedded as resources whose logical name ends in
/// <c>.pub.pem</c>. The verifier accepts the signature if ANY embedded key verifies it,
/// which lets us ship a "current" and "next" key during rotation without breaking
/// existing clients.
///
/// Fail-closed: if no public keys are embedded (e.g. dev forgot to run the signing
/// setup), <see cref="Verify"/> returns false and updates are refused. This is the
/// safe default — see assets/update-keys/README.md for key setup.
/// </summary>
internal static class UpdateVerifier
{
    private static readonly Lazy<IReadOnlyList<string>> EmbeddedPublicKeys = new(LoadEmbeddedPublicKeys);

    public static bool HasAnyKey => EmbeddedPublicKeys.Value.Count > 0;

    public static VerifyResult Verify(byte[] binary, byte[] signature)
    {
        var keys = EmbeddedPublicKeys.Value;
        if (keys.Count == 0)
        {
            DebugLogger.Log("UpdateVerifier: no embedded public keys — update refused (fail-closed).");
            return VerifyResult.NoKeysConfigured;
        }

        foreach (var pem in keys)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(pem);
                if (rsa.VerifyData(binary, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                    return VerifyResult.Ok;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"UpdateVerifier: key load/verify threw: {ex.Message}");
            }
        }

        return VerifyResult.BadSignature;
    }

    private static IReadOnlyList<string> LoadEmbeddedPublicKeys()
    {
        var list = new List<string>();
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (!name.EndsWith(".pub.pem", StringComparison.OrdinalIgnoreCase))
                    continue;
                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null) continue;
                using var reader = new StreamReader(stream);
                list.Add(reader.ReadToEnd());
                DebugLogger.Log($"UpdateVerifier: loaded embedded key '{name}'");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"UpdateVerifier: failed to enumerate embedded keys: {ex.Message}");
        }

        return list;
    }
}

internal enum VerifyResult
{
    Ok,
    BadSignature,
    NoKeysConfigured,
}
