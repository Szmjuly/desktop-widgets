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
        
        var newDeviceId = Guid.NewGuid().ToString();
        
        var directory = Path.GetDirectoryName(DeviceIdFile);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        File.WriteAllText(DeviceIdFile, newDeviceId);
        return newDeviceId;
    }
    
    public static DeviceInfo GetDeviceInfo()
    {
        var deviceId = GetDeviceId();
        var macAddress = GetMacAddress();
        var deviceName = GetDeviceName();
        
        return new DeviceInfo
        {
            DeviceId = deviceId,
            DeviceName = deviceName,
            Platform = Environment.OSVersion.Platform.ToString(),
            PlatformVersion = Environment.OSVersion.Version.ToString(),
            MachineName = Environment.MachineName,
            MacAddress = macAddress,
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
        
        var macAddress = GetMacAddress();
        if (!string.IsNullOrEmpty(macAddress) && macAddress != "unknown")
        {
            components.Add(macAddress);
        }
        
        var combined = string.Join("|", components);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(hash)[..16];
    }
    
    private static string? GetMacAddress()
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
