namespace DesktopHub.Core.Models;

public enum ScanProfileMode
{
    FileBrowser,
    ProjectMode
}

public enum ShortNumberStrategy
{
    Capture,
    Last6Digits,
    Last4Digits,
    BeforeDecimal,
    Full
}

public class ScanProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string RootPath { get; set; } = "";
    public bool Enabled { get; set; } = false;
    public ScanProfileMode Mode { get; set; } = ScanProfileMode.FileBrowser;
    public ProjectPatternConfig? ProjectPatterns { get; set; }
    public string Icon { get; set; } = "📁";
    public int SortOrder { get; set; }

    // "Q" / "P" / "L" / "Archive" for profiles migrated from the legacy drive model. Empty for
    // net-new profiles. Stored on scanned Project.DriveLocation so existing search filters keep
    // working unchanged during the migration window.
    public string LegacyDriveCode { get; set; } = "";
}

public class ProjectPatternConfig
{
    // Regex applied to the names of directories directly under RootPath. Must contain a "year"
    // named capture group. Matches year folders like "_Proj-24", "_Proj-2024", or "2024".
    public string YearDirRegex { get; set; } = @"^_Proj-(?<year>\d{2,4})$";

    // Ordered list of patterns tried against each project folder inside a year directory.
    // First match wins. Each pattern must capture "full_number" and optionally "name" and
    // "short_number".
    public List<ProjectFolderPattern> Patterns { get; set; } = new();
}

public class ProjectFolderPattern
{
    public string Regex { get; set; } = "";

    public ShortNumberStrategy ShortNumberStrategy { get; set; } = ShortNumberStrategy.Capture;

    public string FullNumberPrefix { get; set; } = "";

    public string Description { get; set; } = "";
}
