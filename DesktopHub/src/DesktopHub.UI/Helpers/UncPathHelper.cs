using System;
using System.Runtime.InteropServices;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Resolves mapped drive letters (e.g., Q:\) to their UNC paths (e.g., \\server\share).
/// Uses WNetGetConnection Win32 API.
/// </summary>
internal static class UncPathHelper
{
    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetGetConnection(string localName, char[] remoteName, ref int length);

    private const int NO_ERROR = 0;
    private const int ERROR_MORE_DATA = 234;

    /// <summary>
    /// Converts a local mapped-drive path to its UNC equivalent.
    /// e.g., Q:\_Proj-25\2024278.01 → \\cesflsrv-fsx03\Production\_Proj-25\2024278.01
    /// Returns null if the drive is not a mapped network drive or resolution fails.
    /// </summary>
    public static string? ResolveToUnc(string localPath)
    {
        if (string.IsNullOrEmpty(localPath) || localPath.Length < 2 || localPath[1] != ':')
            return null;

        // Already a UNC path
        if (localPath.StartsWith(@"\\"))
            return localPath;

        var driveLetter = localPath[..2]; // e.g., "Q:"
        var remainder = localPath[2..];   // e.g., "\_Proj-25\2024278.01"

        var uncRoot = GetUncRoot(driveLetter);
        if (uncRoot == null)
            return null;

        return uncRoot + remainder;
    }

    /// <summary>
    /// Gets the UNC root for a drive letter (e.g., "Q:" → "\\cesflsrv-fsx03\Production").
    /// Returns null if the drive is not mapped.
    /// </summary>
    private static string? GetUncRoot(string driveLetter)
    {
        try
        {
            int length = 260;
            var buffer = new char[length];
            int result = WNetGetConnection(driveLetter, buffer, ref length);

            if (result == ERROR_MORE_DATA)
            {
                buffer = new char[length];
                result = WNetGetConnection(driveLetter, buffer, ref length);
            }

            if (result == NO_ERROR)
            {
                return new string(buffer, 0, Array.IndexOf(buffer, '\0'));
            }
        }
        catch
        {
            // Silently fail — drive may not be mapped
        }

        return null;
    }
}
