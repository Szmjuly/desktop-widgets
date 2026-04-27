using System.Security.Cryptography;
using System.Text;

namespace DesktopHub.Core;

/// <summary>
/// Compile-time build configuration flags.
/// Toggle these before building to enable/disable features for different deployment targets.
/// </summary>
public static class BuildConfig
{
    /// <summary>
    /// When true, the Metrics Viewer widget exposes an admin view that aggregates
    /// telemetry data from ALL users (fetched from Firebase).
    /// Set to false for standard user builds.
    /// </summary>
    public static readonly bool IsAdminBuild = false;

    /// <summary>
    /// Tenant this binary belongs to. Baked in at build time and sent with
    /// every <c>issueToken</c> request. The Cloud Function validates it and
    /// hashes the username against the matching tenant salt. Database rules
    /// restrict all user-scoped paths to <c>tenants/{TenantId}/…</c>.
    ///
    /// "ces" is the original DesktopHub deployment. External customer builds
    /// override this constant — a leaked license key from one tenant cannot
    /// authenticate against another tenant's data because the binary is
    /// cryptographically bound to its tenant.
    ///
    /// This is the PLAINTEXT tenant name. It's sent to the issueToken Cloud
    /// Function which hashes it and returns the hash as the token claim.
    /// For DB paths use <see cref="TenantKey"/> instead.
    /// </summary>
    public const string TenantId = "ces";

    /// <summary>
    /// Deterministic hash of <see cref="TenantId"/>. Mirrors the server-side
    /// <c>tenantKeyFor()</c> helper in functions/index.js — MUST stay
    /// byte-for-byte identical so client + server agree on DB paths.
    /// <para>
    /// Used as the segment in every <c>tenants/{TenantKey}/…</c> RTDB path.
    /// The plaintext name never appears in the database.
    /// </para>
    /// </summary>
    public static readonly string TenantKey = ComputeTenantKey(TenantId);

    private static string ComputeTenantKey(string plaintext)
    {
        var input = Encoding.UTF8.GetBytes("dh-tenant-v1:" + (plaintext ?? "").Trim().ToLowerInvariant());
        var digest = SHA256.HashData(input);
        var sb = new StringBuilder(16);
        for (int i = 0; i < 8; i++) sb.AppendFormat("{0:x2}", digest[i]); // first 16 hex chars
        return sb.ToString();
    }
}
