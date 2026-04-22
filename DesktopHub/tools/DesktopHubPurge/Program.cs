using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
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

    [DllImport("kernel32.dll")]
    private static extern int GetConsoleProcessList(int[] processList, int processCount);

    /// <summary>
    /// True when this process owns the console window (i.e. user double-clicked
    /// the EXE in Explorer, as opposed to running it from inside cmd/PowerShell).
    /// When we own the console, Windows destroys the window the moment we exit,
    /// so we must pause before returning or the user sees nothing.
    /// </summary>
    private static bool OwnsConsole()
    {
        try
        {
            var buf = new int[2];
            return GetConsoleProcessList(buf, buf.Length) == 1;
        }
        catch { return false; }
    }

    private static bool _ownsConsole;

    private static int Main(string[] args)
    {
        _ownsConsole = OwnsConsole();
        try
        {
            return RunMain(args);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("=== UNHANDLED ERROR ===");
            Console.WriteLine(ex);
            Console.ResetColor();
            if (_ownsConsole) PauseForUser();
            return 99;
        }
    }

    private static int RunMain(string[] args)
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
            if (_ownsConsole) PauseForUser();
            return 0;
        }

        var isAdmin = IsAdmin();
        var interactive = _ownsConsole;

        Write("================================================================");
        Write($"  DesktopHubPurge v1.0.0  --  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Write("================================================================");
        Write($"  Machine : {Environment.MachineName}");
        Write($"  User    : {Environment.UserName}");
        Write($"  Admin   : {(isAdmin ? "YES" : "NO  (some cleanup steps will be skipped)")}");
        Write($"  Log     : {LogPath}");
        Write("================================================================");
        Write("");

        if (!isAdmin)
        {
            Write("NOTE: Running without admin rights. The following will be SKIPPED:");
            Write("  - HKLM Run entry (machine-wide autostart)");
            Write("  - Program Files install directory");
            Write("  - All-Users Start Menu / Desktop shortcuts");
            Write("  - MSI / Inno system-wide uninstallers");
            Write("");
            Write("HKCU Run entry, LocalAppData, AppData, per-user shortcuts, and");
            Write("the running process WILL still be cleaned up. To do a full");
            Write("cleanup afterward, re-run as Administrator.");
            Write("");
        }

        if (!yes)
        {
            Console.WriteLine("This will forcibly remove DesktopHub files, registry entries,");
            Console.WriteLine("shortcuts, and startup hooks from this machine.");
            if (wipeFirebase)
                Console.WriteLine("It will also attempt to delete the device record from Firebase.");
            Console.Write("Continue? (type YES to proceed, anything else aborts): ");
            var reply = Console.ReadLine();
            if (!string.Equals(reply?.Trim(), "YES", StringComparison.Ordinal))
            {
                Console.WriteLine("Aborted.");
                if (interactive) PauseForUser();
                return 2;
            }
            Console.WriteLine();
        }

        Step("Kill running DesktopHub processes", KillProcesses);
        Step("Run Inno Setup uninstaller (if installed)", () => RunInnoUninstall(isAdmin));
        Step("Run MSI uninstaller (if installed)", () => RunMsiUninstall(isAdmin));
        Step("Remove HKCU Run entry", () => RemoveRunKey(Registry.CurrentUser));
        Step("Remove HKLM Run entry", () => RemoveRunKeyHklm(isAdmin));
        Step("Remove startup-folder shortcuts", RemoveStartupShortcuts);
        Step("Remove Start Menu + Desktop shortcuts", () => RemoveShellShortcuts(isAdmin));
        Step("Delete LocalAppData\\DesktopHub", () => DeleteDir(Environment.SpecialFolder.LocalApplicationData));
        Step("Delete AppData\\DesktopHub", () => DeleteDir(Environment.SpecialFolder.ApplicationData));
        Step("Delete ProgramFiles\\DesktopHub", () => DeleteProgramFilesDir(isAdmin));
        Step("Scrub residual Uninstall entries", () => ScrubUninstallEntries(isAdmin));

        if (wipeFirebase)
            Step("Delete device record from Firebase", () => WipeFirebaseDeviceAsync().GetAwaiter().GetResult());

        WriteLog();

        Console.WriteLine();
        Console.WriteLine("================================================================");
        if (_failures == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(isAdmin
                ? "  All steps completed cleanly."
                : "  Per-user cleanup complete. (Re-run as Admin for system-wide state.)");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  Completed with {_failures} step(s) reporting partial failure.");
            Console.WriteLine($"  See log: {LogPath}");
            Console.ResetColor();
        }
        Console.WriteLine("================================================================");

        if (interactive) PauseForUser();
        return _failures == 0 ? 0 : 1;
    }

    private static void PauseForUser()
    {
        Console.WriteLine();
        Console.Write("Press any key to close this window...");
        try { Console.ReadKey(intercept: true); } catch { Console.ReadLine(); }
        Console.WriteLine();
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

    private static void RunInnoUninstall(bool isAdmin)
    {
        // Inno stores uninstall info under Uninstall\{AppId}_is1.
        // An HKLM install requires admin to uninstall; HKCU per-user installs do not.
        var keyPath = $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{InnoAppId}_is1";
        string? uninstallExe = null;
        bool isPerMachine = false;
        var roots = isAdmin
            ? new[] { Registry.LocalMachine, Registry.CurrentUser }
            : new[] { Registry.CurrentUser };
        foreach (var root in roots)
        {
            using var k = root.OpenSubKey(keyPath);
            uninstallExe = k?.GetValue("UninstallString") as string;
            if (!string.IsNullOrWhiteSpace(uninstallExe))
            {
                isPerMachine = root == Registry.LocalMachine;
                break;
            }
        }
        if (string.IsNullOrWhiteSpace(uninstallExe))
        {
            // Check if there's a per-machine install that we can't touch
            if (!isAdmin)
            {
                using var k = Registry.LocalMachine.OpenSubKey(keyPath);
                if (k != null)
                {
                    Write("  (Inno install is machine-wide -- needs admin; skipped)");
                    return;
                }
            }
            Write("  (no Inno install found)");
            return;
        }

        var exe = uninstallExe.Trim('"');
        Write($"  running {(isPerMachine ? "per-machine" : "per-user")} uninstaller...");
        RunSilent(exe, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART");
    }

    private static void RunMsiUninstall(bool isAdmin)
    {
        if (!isAdmin)
        {
            // Still scan HKCU for per-user MSIs (rare), but skip HKLM entirely.
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (k == null) { Write("  (no MSI install found)"); return; }
            // Fall through with admin-gated scan below limited to HKCU.
        }
        // Scan Uninstall keys for DisplayName == "DesktopHub" (WiX)
        string? productCode = null;
        var uninstallRoots = isAdmin ? UninstallRoots() : UninstallRootsUserOnly();
        foreach (var (root, path) in uninstallRoots)
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
        if (productCode is null)
        {
            if (!isAdmin)
                Write("  (no per-user MSI; machine-wide MSIs need admin; skipped)");
            else
                Write("  (no MSI install found)");
            return;
        }
        RunSilent("msiexec.exe", $"/x {productCode} /qn /norestart");
    }

    private static void RemoveRunKeyHklm(bool isAdmin)
    {
        if (!isAdmin)
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (k?.GetValue(AppName) != null)
                Write("  (entry exists but needs admin to remove; skipped)");
            else
                Write("  (no HKLM entry)");
            return;
        }
        RemoveRunKey(Registry.LocalMachine);
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

    private static void RemoveShellShortcuts(bool isAdmin)
    {
        var paths = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{AppName}.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), $"{AppName}.lnk"),
        };
        var groupDirs = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName),
        };
        var quickLaunch = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Internet Explorer", "Quick Launch", $"{AppName}.lnk");
        paths.Add(quickLaunch);

        if (isAdmin)
        {
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), $"{AppName}.lnk"));
            paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), $"{AppName}.lnk"));
            groupDirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), AppName));
        }
        else
        {
            Write("  (skipping All-Users shortcuts -- needs admin)");
        }

        DeleteFiles(paths);
        foreach (var d in groupDirs) DeleteDirectory(d);
    }

    private static void DeleteDir(Environment.SpecialFolder sf)
    {
        var dir = Path.Combine(Environment.GetFolderPath(sf), AppName);
        DeleteDirectory(dir);
    }

    private static void DeleteProgramFilesDir(bool isAdmin)
    {
        if (!isAdmin)
        {
            var pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);
            if (Directory.Exists(pf))
                Write("  (Program Files\\DesktopHub exists but needs admin to delete; skipped)");
            else
                Write("  (no Program Files\\DesktopHub)");
            return;
        }
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), AppName),
        };
        foreach (var c in candidates) DeleteDirectory(c);
    }

    private static void ScrubUninstallEntries(bool isAdmin)
    {
        // Remove any stray Uninstall entry whose DisplayName is DesktopHub
        // (happens if Inno uninstaller was skipped but left a key behind)
        var roots = isAdmin ? UninstallRoots() : UninstallRootsUserOnly();
        foreach (var (root, path) in roots)
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

    private static IEnumerable<(RegistryKey root, string path)> UninstallRootsUserOnly()
    {
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
