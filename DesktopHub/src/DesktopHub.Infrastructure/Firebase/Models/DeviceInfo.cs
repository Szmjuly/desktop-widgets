namespace DesktopHub.Infrastructure.Firebase.Models;

public class DeviceInfo
{
    public required string DeviceId { get; set; }
    public required string DeviceName { get; set; }
    public required string Platform { get; set; }
    public required string PlatformVersion { get; set; }
    public required string MachineName { get; set; }
    public string? MacAddress { get; set; }
    public required string ProcessorArchitecture { get; set; }
    
    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            ["device_id"] = DeviceId,
            ["device_name"] = DeviceName,
            ["platform"] = Platform,
            ["platform_version"] = PlatformVersion,
            ["machine_name"] = MachineName,
            ["mac_address"] = MacAddress ?? "unknown",
            ["processor_architecture"] = ProcessorArchitecture
        };
    }
}
