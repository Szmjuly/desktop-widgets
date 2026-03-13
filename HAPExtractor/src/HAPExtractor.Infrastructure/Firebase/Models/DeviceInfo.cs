namespace HAPExtractor.Infrastructure.Firebase.Models;

public class DeviceInfo
{
    public required string DeviceId { get; set; }
    public required string DeviceName { get; set; }
    public string? Platform { get; set; }
    public string? PlatformVersion { get; set; }
    public string? MachineName { get; set; }
    public string? MacAddress { get; set; }
    public string? ProcessorArchitecture { get; set; }
}
