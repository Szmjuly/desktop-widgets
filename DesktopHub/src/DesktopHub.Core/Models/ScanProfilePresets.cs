namespace DesktopHub.Core.Models;

public enum ScanProfilePresetId
{
    Personal,
    CES,
    GenericNumbered,
    Blank
}

public static class ScanProfilePresets
{
    public static List<ScanProfile> CES()
    {
        return new List<ScanProfile>
        {
            new ScanProfile
            {
                Name = "Florida",
                RootPath = @"Q:\",
                Enabled = false,
                Mode = ScanProfileMode.ProjectMode,
                Icon = "🌴",
                SortOrder = 0,
                LegacyDriveCode = "Q",
                ProjectPatterns = new ProjectPatternConfig
                {
                    YearDirRegex = @"^_Proj-(?<year>\d{2,4})$",
                    Patterns = new List<ProjectFolderPattern>
                    {
                        new ProjectFolderPattern
                        {
                            Description = "CES Q new format (P250784.00 - Project Name)",
                            Regex = @"^P(?<full_number>\d{6}\.?\d*)(\s*-\s*)?(?<name>.+)?$",
                            FullNumberPrefix = "P",
                            ShortNumberStrategy = ShortNumberStrategy.Last6Digits,
                        },
                        new ProjectFolderPattern
                        {
                            Description = "CES Q old format (2024638.001 Project Name)",
                            Regex = @"^(?<full_number>\d{4}\d{3}\.?\d*)(\s+)?(?<name>.+)?$",
                            ShortNumberStrategy = ShortNumberStrategy.Last4Digits,
                        }
                    }
                }
            },
            new ScanProfile
            {
                Name = "Connecticut",
                RootPath = @"P:\",
                Enabled = false,
                Mode = ScanProfileMode.ProjectMode,
                Icon = "🍁",
                SortOrder = 1,
                LegacyDriveCode = "P",
                ProjectPatterns = new ProjectPatternConfig
                {
                    YearDirRegex = @"^_Proj-(?<year>\d{2,4})$",
                    Patterns = new List<ProjectFolderPattern>
                    {
                        new ProjectFolderPattern
                        {
                            Description = "CES CT format (2019038.00 Project Name)",
                            Regex = @"^(?<full_number>\d{7}\.\d{2})(\s*-?\s*)?(?<name>.+)?$",
                            ShortNumberStrategy = ShortNumberStrategy.Last6Digits,
                        }
                    }
                }
            },
            new ScanProfile
            {
                Name = "CT Legacy",
                RootPath = @"L:\",
                Enabled = false,
                Mode = ScanProfileMode.ProjectMode,
                Icon = "📜",
                SortOrder = 2,
                LegacyDriveCode = "L",
                ProjectPatterns = new ProjectPatternConfig
                {
                    YearDirRegex = @"^_Proj-(?<year>\d{2,4})$",
                    Patterns = new List<ProjectFolderPattern>
                    {
                        new ProjectFolderPattern
                        {
                            Description = "CES CT Legacy (same format as P)",
                            Regex = @"^(?<full_number>\d{7}\.\d{2})(\s*-?\s*)?(?<name>.+)?$",
                            ShortNumberStrategy = ShortNumberStrategy.Last6Digits,
                        }
                    }
                }
            },
            new ScanProfile
            {
                Name = "CT Archive",
                RootPath = "",
                Enabled = false,
                Mode = ScanProfileMode.ProjectMode,
                Icon = "🗄",
                SortOrder = 3,
                LegacyDriveCode = "Archive",
                ProjectPatterns = new ProjectPatternConfig
                {
                    YearDirRegex = @"^_Proj-(?<year>\d{2,4})$",
                    Patterns = new List<ProjectFolderPattern>
                    {
                        new ProjectFolderPattern
                        {
                            Description = "CES CT Archive (same format as P)",
                            Regex = @"^(?<full_number>\d{7}\.\d{2})(\s*-?\s*)?(?<name>.+)?$",
                            ShortNumberStrategy = ShortNumberStrategy.Last6Digits,
                        }
                    }
                }
            },
        };
    }

    public static List<ScanProfile> Personal()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return new List<ScanProfile>
        {
            new ScanProfile
            {
                Name = "Documents",
                RootPath = docs,
                Enabled = !string.IsNullOrEmpty(docs) && Directory.Exists(docs),
                Mode = ScanProfileMode.FileBrowser,
                Icon = "📁",
                SortOrder = 0,
            }
        };
    }

    public static List<ScanProfile> GenericNumberedProjects()
    {
        return new List<ScanProfile>
        {
            new ScanProfile
            {
                Name = "Projects",
                RootPath = "",
                Enabled = false,
                Mode = ScanProfileMode.ProjectMode,
                Icon = "📊",
                SortOrder = 0,
                ProjectPatterns = new ProjectPatternConfig
                {
                    YearDirRegex = @"^(?<year>\d{4})$",
                    Patterns = new List<ProjectFolderPattern>
                    {
                        new ProjectFolderPattern
                        {
                            Description = "YYYY-NNN format",
                            Regex = @"^(?<full_number>\d{4}-\d{3})(\s*[-_]\s*)?(?<name>.+)?$",
                            ShortNumberStrategy = ShortNumberStrategy.BeforeDecimal,
                        }
                    }
                }
            }
        };
    }

    public static List<ScanProfile> Blank() => new List<ScanProfile>();

    public static List<ScanProfile> ForPresetId(ScanProfilePresetId id) => id switch
    {
        ScanProfilePresetId.Personal => Personal(),
        ScanProfilePresetId.CES => CES(),
        ScanProfilePresetId.GenericNumbered => GenericNumberedProjects(),
        ScanProfilePresetId.Blank => Blank(),
        _ => Blank()
    };
}
