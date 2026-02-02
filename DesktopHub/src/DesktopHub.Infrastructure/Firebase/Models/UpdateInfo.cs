namespace DesktopHub.Infrastructure.Firebase.Models;

public class UpdateInfo
{
    public required string LatestVersion { get; set; }
    public required string CurrentVersion { get; set; }
    public bool UpdateAvailable => IsNewerVersion(LatestVersion, CurrentVersion);
    public string? ReleaseNotes { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public bool RequiredUpdate { get; set; }
    
    private static bool IsNewerVersion(string latest, string current)
    {
        if (!Version.TryParse(latest, out var latestVer) || 
            !Version.TryParse(current, out var currentVer))
        {
            return false;
        }
        return latestVer > currentVer;
    }
}
