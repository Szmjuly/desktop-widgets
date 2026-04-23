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
    /// </summary>
    public const string TenantId = "ces";
}
