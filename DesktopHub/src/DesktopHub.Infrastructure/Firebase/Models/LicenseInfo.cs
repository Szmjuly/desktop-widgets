namespace DesktopHub.Infrastructure.Firebase.Models;

public class LicenseInfo
{
    public required string LicenseKey { get; set; }
    public required string AppId { get; set; }
    public required string Plan { get; set; }
    public required string Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int MaxDevices { get; set; }
    public bool IsActive => Status == "active" && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}
