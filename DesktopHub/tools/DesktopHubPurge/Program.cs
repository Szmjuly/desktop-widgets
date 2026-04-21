using System.Diagnostics;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

[assembly: SupportedOSPlatform("windows")]

namespace DesktopHub.Purge;

internal static class Program
{
    private const string AppName = "DesktopHub";
    private const string ExeName = "DesktopHub.exe";
    private const string InnoAppId = "{8F2A3B4C-5D6E-7F8A-9B0C-1D2E3F4A5B6C}";
    private const string RtdbBaseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com";

    private static readonly StringBuilder Log = new();
    private static int _failures;

    private static int Main(string[] args)
    {
        var yes = args.Contains("--yes", StringComparer.OrdinalIgnoreCase) ||
                  args.Contains("-y", StringComparer.OrdinalIgnoreCase);
        var wipeFirebase = args.Contains("--wipe-firebase", StringComparer.OrdinalIgnoreCase);
        var help = args.Contains("--help", StringComparer.OrdinalIgnoreCase) ||
                   args.Contains("-h", StringComparer.OrdinalIgnoreCase) ||
                   args.Contains("/?", StringComparer.OrdinalIgnoreCase);

        if (help)
        {
            PrintUsage();
            return 0;
        }

        Write($"DesktopHubPurge v1.0.0  —  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Write($"Admin: {IsAdmin()}   Machine: {Environment.MachineName}   User: {Environment.UserName}");
        Write("");

        if (!yes)
        {
            Console.WriteLine("This will forcibly remove ALL DesktopHub files, registry entries,");
            Console.WriteLine("shortcuts, and startup hooks from this machine.");
            if (wipeFirebase)
                Console.WriteLine("It will also attempt to delete the device record from Firebase.");
            Console.Write("Continue? (type YES to proceed): ");
            var reply = Console.ReadLine();
            if (!string.Equals(reply?.Trim(), "YES", StringComparison.Ordinal))
            {
                Console.WriteLine("Aborted.");
                return 2;
            }
        }

        Step("Kill running DesktopHub processes", KillProcesses);
        Step("Run Inno Setup uninstaller (if installed)", RunInnoUninstall);
        Step("Run MSI uninstaller (if installed)", RunMsiUninstall);
        Step("Remove HKCU Run entry", () => RemoveRunKey(Registry.CurrentUser));
        Step("Remove HKLM Run entry", () => RemoveRunKey(Registry.LocalMachine));
        Step("Remove startup-folder shortcuts", RemoveStartupShortcuts);
        Step("Remove Start Menu + Desktop shortcuts", RemoveShellShortcuts);
        Step("Delete LocalAppData\\DesktopHub", () => DeleteDir(Environment.SpecialFolder.LocalApplicationData));
        Step("Delete AppData\\DesktopHub", () => DeleteDir(Environment.SpecialFolder.ApplicationData));
        Step("Delete ProgramFiles\\DesktopHub", DeleteProgramFilesDir);
        Step("Scrub residual Uninstall entries", ScrubUninstallEntries);

        if (wipeFirebase)
            Step("Delete device record from Firebase", () => WipeFirebaseDeviceAsync().GetAwaiter().GetResult());

        WriteLog();

        Console.WriteLine();
        Console.WriteLine(_failures == 0
            ? "All steps completed cleanly."
            : $"Completed with {_failures} step(s) reporting partial failure. See log.");
        Console.WriteLine($"Log: {LogPath}");
        return _failures == 0 ? 0 : 1;
    }

    // ──────────────────────── steps ────────────────────────

    private static void KillProcesses()
    {
        var procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExeName));
        if (procs.Length == 0) { Write("  (no running processes)"); return; }
        foreach (var p in procs)
        {
            try
            {
                p.Kill(entireProcessTree: true);
                p.WaitForExit(5000);
                Write($"  killed PID {p.Id}");
            }
            catch (Exception ex) { Fail($"  could not kill PID {p.Id}: {ex.Message}"); }
        }
    }

    private static void RunInnoUninstall()
    {
        // Inno stores uninstall info under Uninstall\{AppId}_is1
        var keyPath = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{InnoAppId}_is1";
        string? uninstallExe = null;
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var k = root.OpenSubKey(keyPath);
            uninstallExe = k?.GetValue("UninstallString") as string;
            if (!string.IsNullOrWhiteSpace(uninstallExe)) break;
        }
        if (string.IsNullOrWhiteSpace(uninstallExe)) { Write("  (no Inno install found)"); return; }

        // UninstallString is wrapped in quotes; strip and append silent flags
        var exe = uninstallExe.Trim('"');
        RunSilent(exe, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART");
    }

    private static void RunMsiUninstall()
    {
        // Scan Uninstall keys for DisplayName == "DesktopHub" (WiX)
        string? productCode = null;
        foreach (var (root, path) in UninstallRoots())
        {
            using var k = root.OpenSubKey(path);
            if (k is null) continue;
            foreach (var sub in k.GetSubKeyNames())
            {
                if (!sub.StartsWith("{") || !sub.EndsWith("}")) continue; // MSI product codes are GUIDs
                using var s = k.OpenSubKey(sub);
                var name = s?.GetValue("DisplayName") as string;
                var publisher = s?.GetValue("Publisher") as string;
                if (string.Equals(name, AppName, StringComparison.OrdinalIgnoreCase))
                {
                    // Guard against accidentally matching something unrelated named DesktopHub
                    if (publisher is null || publisher.Contains("DesktopHub", StringComparison.OrdinalIgnoreCase) || publisher.Length == 0)
                    {
                        productCode = sub;
                        break;
                    }
                }
            }
            if (productCode != null) break;
        }
        if (productCode is null) { Write("  (no MSI install found)"); return; }
        RunSilent("msiexec.exe", $"/x {productCode} /qn /norestart");
    }

    private static void RemoveRunKey(RegistryKey root)
    {
        const string path = @"Software\Microsoft\Windows\CurrentVersion\Run";
        try
        {
            using var k = root.OpenSubKey(path, writable: true);
            if (k is null) { Write("  (Run key absent)"); return; }
            if (k.GetValue(AppName) is null) { Write("  (no DesktopHub entry)"); return; }
            k.DeleteValue(AppName, throwOnMissingValue: false);
            Write("  removed Run entry");
        }
        catch (UnauthorizedAccessException) { Fail("  insufficient rights (try running as Administrator)"); }
        catch (Exception ex) { Fail($"  {ex.Message}"); }
    }

    private static void RemoveStartupShortcuts()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), $"{AppName}.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), $"{AppName}.lnk"),
        };
        DeleteFiles(candidates);
    }

    private static void RemoveShellShortcuts()
    {
        var paths = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{AppName}.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), $"{AppName}.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), $"{AppName}.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), $"{AppName}.lnk"),
        };
        var groupDirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), AppName),
        };
        var quickLaunch = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Internet Explorer", "Quick Launch", $"{AppName}.lnk");
        paths.Add(quickLaunch);

        DeleteFiles(paths);
        foreach (var d in groupDirs) DeleteDirectory(d);
    }

    private static void DeleteDir(Environment.SpecialFolder sf)
    {
        var dir = Path.Combine(Environment.GetFolderPath(sf), AppName);
        DeleteDirectory(dir);
    }

    private static void DeleteProgramFilesDir()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), AppName),
        };
        foreach (var c in candidates) DeleteDirectory(c);
    }

    private static void ScrubUninstallEntries()
    {
        // Remove any stray Uninstall entry whose DisplayName is DesktopHub
        // (happens if Inno uninstaller was skipped but left a key behind)
        foreach (var (root, path) in UninstallRoots())
        {
            try
            {
                using var k = root.OpenSubKey(path, writable: true);
                if (k is null) continue;
                foreach (var sub in k.GetSubKeyNames())
                {
                    using var s = k.OpenSubKey(sub);
                    var name = s?.GetValue("DisplayName") as string;
                    if (string.Equals(name, AppName, StringComparison.OrdinalIgnoreCase))
                    {
                        s?.Close();
                        k.DeleteSubKeyTree(sub, throwOnMissingSubKey: false);
                        Write($"  removed Uninstall\\{sub}");
                    }
                }
            }
            catch (UnauthorizedAccessException) { /* expected for HKLM without admin */ }
            catch (Exception ex) { Fail($"  {ex.Message}"); }
        }
    }

    private static async Task WipeFirebaseDeviceAsync()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var deviceIdPath = Path.Combine(localAppData, AppName, "device_id.txt");
        if (!File.Exists(deviceIdPath))
        {
            Write("  (no device_id.txt — likely already wiped or never initialized)");
            return;
        }
        var deviceId = (await File.ReadAllTextAsync(deviceIdPath)).Trim();
        if (string.IsNullOrWhiteSpace(deviceId)) { Write("  (device_id empty)"); return; }

        // Best-effort: unauthenticated DELETE will fail against production rules.
        // The user is expected to run push-update.ps1-equivalent cleanup from a dev machine for server-side state.
        // We still issue the DELETE here in case the project has lax rules, and we log the result either way.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            var resp = await http.DeleteAsync($"{RtdbBaseUrl}/devices/{deviceId}.json");
            if (resp.IsSuccessStatusCode)
                Write($"  deleted devices/{deviceId}");
            else
                Fail($"  DELETE devices/{deviceId} returned {(int)resp.StatusCode} (expected against auth rules; ask dev to prune server-side)");
        }
        catch (Exception ex) { Fail($"  {ex.Message}"); }
    }

    // ──────────────────────── helpers ────────────────────────

    private static IEnumerable<(RegistryKey root, string path)> UninstallRoots()
    {
        yield return (Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
        yield return (Registry.LocalMachine, @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        yield return (Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
    }

    private static void DeleteFiles(IEnumerable<string> paths)
    {
        foreach (var p in paths)
        {
            if (!File.Exists(p)) continue;
            try { File.Delete(p); Write($"  deleted {p}"); }
            catch (Exception ex) { Fail($"  {p}: {ex.Message}"); }
        }
    }

    private static void DeleteDirectory(string dir)
    {
        if (!Directory.Exists(dir)) { Write($"  (absent: {dir})"); return; }
        try
        {
            // Clear read-only flags that shortcut files sometimes get
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(f, FileAttributes.Normal); } catch { /* ignore */ }
            }
            Directory.Delete(dir, recursive: true);
            Write($"  deleted {dir}");
        }
        catch (Exception ex) { Fail($"  {dir}: {ex.Message}"); }
    }

    private static void RunSilent(string exe, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi)!;
            p.WaitForExit(120_000);
            if (p.ExitCode != 0 && p.ExitCode != 1605 /* MSI: unknown product */)
                Fail($"  `{exe} {args}` exited {p.ExitCode}");
            else
                Write($"  `{exe} {args}` exited {p.ExitCode}");
        }
        catch (Exception ex) { Fail($"  could not run `{exe}`: {ex.Message}"); }
    }

    private static bool IsAdmin()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private static void Step(string title, Action body)
    {
        Write($"[{title}]");
        try { body(); }
        catch (Exception ex) { Fail($"  uncaught: {ex.Message}"); }
    }

    private static void Write(string line)
    {
        Console.WriteLine(line);
        Log.AppendLine(line);
    }

    private static void Fail(string line)
    {
        _failures++;
        Write(line);
    }

    private static readonly string LogPath =
        Path.Combine(Path.GetTempPath(), $"desktophub-purge-{DateTime.Now:yyyyMMdd-HHmmss}.log");

    private static void WriteLog()
    {
        try { File.WriteAllText(LogPath, Log.ToString()); }
        catch { /* ignore */ }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("DesktopHubPurge — forcibly remove all DesktopHub state from this machine.");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  DesktopHubPurge.exe [--yes] [--wipe-firebase]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --yes, -y           Skip the interactive confirmation prompt.");
        Console.WriteLine("  --wipe-firebase     Also attempt to DELETE the device record server-side.");
        Console.WriteLine("                      (Requires the server's rules to allow it, otherwise");
        Console.WriteLine("                      ask a developer to prune the record from the dashboard.)");
        Console.WriteLine("  --help, -h          Show this help.");
        Console.WriteLine();
        Console.WriteLine("Exit codes:");
        Console.WriteLine("  0  All steps clean.");
        Console.WriteLine("  1  One or more steps reported partial failure (see log).");
        Console.WriteLine("  2  User aborted at the confirmation prompt.");
        Console.WriteLine();
        Console.WriteLine("Run as Administrator to clean HKLM entries and Program Files.");
    }
}
