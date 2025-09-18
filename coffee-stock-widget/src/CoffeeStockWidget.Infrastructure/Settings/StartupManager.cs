using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CoffeeStockWidget.Infrastructure.Settings;

[SupportedOSPlatform("windows")]
public static class StartupManager
{
    private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string AppValueName = "CoffeeStockWidget";

    public static Task<bool> IsEnabledAsync()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var val = key?.GetValue(AppValueName) as string;
        return Task.FromResult(!string.IsNullOrEmpty(val));
    }

    public static Task EnableAsync(bool enable)
    {
        if (enable)
        {
            var exe = Process.GetCurrentProcess().MainModule?.FileName ?? Environment.ProcessPath ?? string.Empty;
            if (string.IsNullOrEmpty(exe)) return Task.CompletedTask;
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            key.SetValue(AppValueName, '"' + exe + '"');
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(AppValueName, throwOnMissingValue: false);
        }
        return Task.CompletedTask;
    }
}
