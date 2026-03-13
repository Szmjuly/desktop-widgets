namespace HAPExtractor.Infrastructure.Firebase.Models;

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
        if (!Version.TryParse(StripNonNumericSuffix(latest), out var latestVer) ||
            !Version.TryParse(StripNonNumericSuffix(current), out var currentVer))
        {
            return false;
        }
        return latestVer > currentVer;
    }

    /// <summary>
    /// Strips trailing non-numeric dot-segments from a version string so that
    /// "1.0.2.HAP" → "1.0.2" and can be parsed by Version.TryParse.
    /// </summary>
    private static string StripNonNumericSuffix(string version)
    {
        var parts = version.Split('.');
        var numericParts = new List<string>();
        foreach (var part in parts)
        {
            if (int.TryParse(part, out _))
                numericParts.Add(part);
            else
                break; // stop at first non-numeric segment
        }
        return numericParts.Count > 0 ? string.Join(".", numericParts) : version;
    }
}
