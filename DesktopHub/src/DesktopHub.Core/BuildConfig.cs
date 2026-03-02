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
    public static readonly bool IsAdminBuild = true;
}
