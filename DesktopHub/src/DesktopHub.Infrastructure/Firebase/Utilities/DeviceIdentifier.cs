using System.Management;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using DesktopHub.Infrastructure.Firebase.Models;

namespace DesktopHub.Infrastructure.Firebase.Utilities;

public static class DeviceIdentifier
{
    private static readonly string DeviceIdFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DesktopHub",
        "device_id.txt"
    );

    // Salt for hashing any PII value that leaves the machine. Bumping the "v"
    // intentionally invalidates previously hashed values on the server side if
    // we ever need to break linkage (e.g. a privacy incident).
    // This salt does NOT need to be secret -- hashed values are still
    // deterministic per-device, which is required for duplicate detection.
    private const string PiiHashSalt = "DesktopHub|pii|v1|2026";

    /// <summary>
    /// Returns a short deterministic hash of a PII value. Uses a salt + SHA256
    /// and truncates to 16 hex chars (64 bits of entropy). The same raw value
    /// always hashes the same way, so dev tools can still group by "this
    /// machine's MAC" without ever handling the raw MAC address.
    /// </summary>
    public static string HashPiiValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || string.Equals(raw, "unknown", StringComparison.OrdinalIgnoreCase))
            return "unknown";

        var input = PiiHashSalt + "|" + raw.Trim();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
    
    public static string GetDeviceId()
    {
        if (File.Exists(DeviceIdFile))
        {
            var existingId = File.ReadAllText(DeviceIdFile).Trim();
            if (!string.IsNullOrWhiteSpace(existingId))
            {
                return existingId;
            }
        }

        // Derive a deterministic ID from hardware so reinstalls produce the same ID.
        // This uses the RAW MAC because changing the derivation here would break
        // backward compat for every device that already has a persisted ID. The
        // raw MAC never leaves the machine -- only the resulting GUID does.
        var mac = GetMacAddressRaw() ?? "unknown";
        var stable = $"{Environment.MachineName}|{mac}|{Environment.UserName}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stable));
        var newDeviceId = new Guid(hash[..16]).ToString();

        var directory = Path.GetDirectoryName(DeviceIdFile);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(DeviceIdFile, newDeviceId);
        return newDeviceId;
    }
    
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public static DeviceInfo GetDeviceInfo()
    {
        var deviceId = GetDeviceId();
        // IMPORTANT: only the HASHED MAC leaves this machine. Raw MAC stays
        // local (used by GetDeviceId for deterministic hardware-based IDs).
        // The hash is deterministic so Dev Panel "group duplicates by MAC"
        // still works; the network just never sees the actual hardware addr.
        var macHash = HashPiiValue(GetMacAddressRaw());
        var deviceName = GetDeviceName();

        return new DeviceInfo
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            Platform = Environment.OSVersion.Platform.ToString(),
            PlatformVersion = Environment.OSVersion.Version.ToString(),
            MachineName = Environment.MachineName,
            MacAddress = macHash,
            ProcessorArchitecture = Environment.Is64BitOperatingSystem ? "x64" : "x86"
        };
    }
    
    public static string GetUserIdentifier(string deviceId, string? licenseKey = null)
    {
        var components = new List<string> { deviceId };
        
        if (!string.IsNullOrEmpty(licenseKey))
        {
            components.Add(licenseKey);
        }
        
        // GetUserIdentifier is itself only used inside a SHA256, so it's fine
        // to consume the raw MAC here -- the final identifier is a hash and
        // never leaks the raw value.
        var macAddress = GetMacAddressRaw();
        if (!string.IsNullOrEmpty(macAddress) && macAddress != "unknown")
        {
            components.Add(macAddress);
        }
        
        var combined = string.Join("|", components);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash)[..16];
    }
    
    /// <summary>
    /// INTERNAL ONLY. Returns the raw MAC address of the first operational
    /// NIC. This value must never leave the local machine -- it's used only
    /// to derive deterministic hashes (device_id, PII hashes). All consumers
    /// that need a stable machine identifier should use the hashed form via
    /// <see cref="GetDeviceInfo"/>.
    /// </summary>
    private static string? GetMacAddressRaw()
    {
        try
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && 
                           n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderBy(n => n.NetworkInterfaceType)
                .ToList();
            
            var nic = nics.FirstOrDefault();
            if (nic != null)
            {
                var macBytes = nic.GetPhysicalAddress().GetAddressBytes();
                return string.Join(":", macBytes.Select(b => b.ToString("X2")));
            }
        }
        catch
        {
            // Fall back to alternative method
        }
        
        return GetMacAddressFallback();
    }
    
    private static string? GetMacAddressFallback()
    {
        try
        {
            var machineInfo = $"{Environment.MachineName}{Environment.ProcessorCount}{Environment.UserName}";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(machineInfo));
            return string.Join(":", hash.Take(6).Select(b => b.ToString("X2")));
        }
        catch
        {
            return "unknown";
        }
    }
    
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string GetDeviceName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                var model = obj["Model"]?.ToString();
                var manufacturer = obj["Manufacturer"]?.ToString();
                if (!string.IsNullOrEmpty(model) && !string.IsNullOrEmpty(manufacturer))
                {
                    return $"{manufacturer} {model}";
                }
            }
        }
        catch
        {
            // Fall back to machine name
        }
        
        return Environment.MachineName;
    }
}
