using System.IO;
using System.Runtime.InteropServices;

namespace ProjectSearcher.UI.Helpers;

public static class StartMenuHelper
{
    public static void CreateStartMenuShortcut()
    {
        try
        {
            var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var shortcutFolder = Path.Combine(startMenuPath, "Project Searcher");
            var shortcutPath = Path.Combine(shortcutFolder, "Project Searcher.lnk");

            // Create folder if it doesn't exist
            if (!Directory.Exists(shortcutFolder))
            {
                Directory.CreateDirectory(shortcutFolder);
            }

            // Only create shortcut if it doesn't already exist
            if (!System.IO.File.Exists(shortcutPath))
            {
                // Use dynamic COM interop which works with .NET Core
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    DebugLogger.Log("StartMenuHelper: Failed to get WScript.Shell type");
                    return;
                }

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null)
                {
                    DebugLogger.Log("StartMenuHelper: Failed to create WScript.Shell instance");
                    return;
                }

                try
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    shortcut.WorkingDirectory = Path.GetDirectoryName(shortcut.TargetPath) ?? "";
                    shortcut.Description = "Quick project search tool";
                    shortcut.Save();

                    DebugLogger.Log($"StartMenuHelper: Created Start Menu shortcut at {shortcutPath}");
                }
                finally
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
            else
            {
                DebugLogger.Log("StartMenuHelper: Start Menu shortcut already exists");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"StartMenuHelper: Failed to create Start Menu shortcut: {ex.Message}");
        }
    }

    public static void RemoveStartMenuShortcut()
    {
        try
        {
            var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var shortcutFolder = Path.Combine(startMenuPath, "Project Searcher");

            if (Directory.Exists(shortcutFolder))
            {
                Directory.Delete(shortcutFolder, true);
                DebugLogger.Log("StartMenuHelper: Removed Start Menu shortcut");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"StartMenuHelper: Failed to remove Start Menu shortcut: {ex.Message}");
        }
    }
}
