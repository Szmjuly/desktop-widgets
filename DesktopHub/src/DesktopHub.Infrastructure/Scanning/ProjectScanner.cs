using System.Text.RegularExpressions;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;

namespace DesktopHub.Infrastructure.Scanning;

/// <summary>
/// Scans Q: and P: drives for project folders using regex patterns
/// </summary>
public class ProjectScanner : IProjectScanner
{
    // Q Drive - Old format: 2024638.001 Project Name
    private static readonly Regex QDriveOldProjectPattern = new(
        @"^(?<full_number>\d{4})(?<seq>\d{3}\.?\d*)(\s+)?(?<name>.+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // Q Drive - New format: P250784.00 - Project Name
    private static readonly Regex QDriveNewProjectPattern = new(
        @"^P(?<number>\d{6}\.?\d*)(\s*-\s*)?(?<name>.+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // P Drive - format: 2019038.00 Malden City Hall Cx (YYYYNNN.NN - 7 digits before decimal)
    private static readonly Regex PDriveProjectPattern = new(
        @"^(?<number>\d{7}\.\d{2})(\s*-?\s*)?(?<name>.+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // Year directory pattern: _Proj-24, _Proj-2024, etc.
    private static readonly Regex YearDirPattern = new(
        @"^_Proj-(?<year>\d{2,4})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public async Task<List<Project>> ScanProjectsAsync(string drivePath, CancellationToken cancellationToken = default)
    {
        var driveLetter = Path.GetPathRoot(drivePath)?.TrimEnd('\\').TrimEnd(':');
        return await ScanProjectsAsync(drivePath, driveLetter ?? "Q", cancellationToken);
    }

    public async Task<List<Project>> ScanProjectsAsync(string drivePath, string driveLocation, CancellationToken cancellationToken = default)
    {
        var projects = new List<Project>();

        if (!Directory.Exists(drivePath))
        {
            throw new DirectoryNotFoundException($"{driveLocation}: drive not found at {drivePath}");
        }

        // Find all year directories (_Proj-24, _Proj-2024, etc.)
        var yearDirectories = Directory.GetDirectories(drivePath)
            .Where(dir => YearDirPattern.IsMatch(Path.GetFileName(dir)))
            .ToList();

        foreach (var yearDir in yearDirectories)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var yearProjects = await ScanYearDirectoryAsync(yearDir, driveLocation, cancellationToken);
            projects.AddRange(yearProjects);
        }

        return projects;
    }

    public async Task<List<Project>> ScanYearDirectoryAsync(string yearDirectoryPath, CancellationToken cancellationToken = default)
    {
        var driveLetter = Path.GetPathRoot(yearDirectoryPath)?.TrimEnd('\\').TrimEnd(':');
        return await ScanYearDirectoryAsync(yearDirectoryPath, driveLetter ?? "Q", cancellationToken);
    }

    public async Task<List<Project>> ScanYearDirectoryAsync(string yearDirectoryPath, string driveLocation, CancellationToken cancellationToken = default)
    {
        var projects = new List<Project>();

        if (!Directory.Exists(yearDirectoryPath))
        {
            return projects;
        }

        // Extract year from directory name
        var yearDirName = Path.GetFileName(yearDirectoryPath);
        var yearMatch = YearDirPattern.Match(yearDirName);
        if (!yearMatch.Success)
        {
            return projects;
        }

        var year = yearMatch.Groups["year"].Value;
        // Convert 2-digit year to 4-digit (24 -> 2024)
        if (year.Length == 2)
        {
            year = "20" + year;
        }

        // Scan all subdirectories in this year folder
        await Task.Run(() =>
        {
            try
            {
                var directories = Directory.GetDirectories(yearDirectoryPath);
                foreach (var dir in directories)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var dirName = Path.GetFileName(dir);
                    var project = TryParseProjectFolder(dirName, dir, year, driveLocation);
                    if (project != null)
                    {
                        projects.Add(project);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
            catch (Exception)
            {
                // Log but continue scanning
            }
        }, cancellationToken);

        return projects;
    }

    public Project? TryParseProjectFolder(string directoryName, string fullPath, string year)
    {
        var driveLetter = Path.GetPathRoot(fullPath)?.TrimEnd('\\').TrimEnd(':');
        return TryParseProjectFolder(directoryName, fullPath, year, driveLetter ?? "Q");
    }

    public Project? TryParseProjectFolder(string directoryName, string fullPath, string year, string driveLocation)
    {
        // P Drive, L Drive (Legacy), and Archive drive use the same naming pattern
        if (driveLocation is "P" or "L" or "Archive")
        {
            var pMatch = PDriveProjectPattern.Match(directoryName);
            if (pMatch.Success)
            {
                var number = pMatch.Groups["number"].Value;
                var name = pMatch.Groups["name"].Value?.Trim() ?? string.Empty;

                // Generate short number (last 6 digits)
                var shortNumber = number.Replace(".", "");
                if (shortNumber.Length >= 6)
                    shortNumber = shortNumber.Substring(shortNumber.Length - 6);

                return new Project
                {
                    Id = GenerateProjectId(fullPath),
                    FullNumber = number,
                    ShortNumber = shortNumber,
                    Name = name,
                    Path = fullPath,
                    Year = year,
                    DriveLocation = driveLocation,
                    LastScanned = DateTime.UtcNow
                };
            }
        }
        else // Q Drive patterns
        {
            // Try new format first (P250784.00 - Name)
            var newMatch = QDriveNewProjectPattern.Match(directoryName);
            if (newMatch.Success)
            {
                var number = newMatch.Groups["number"].Value;
                var name = newMatch.Groups["name"].Value?.Trim() ?? string.Empty;

                // Generate short number (last 6 digits)
                var shortNumber = number.Length >= 6 ? number.Substring(number.Length - 6) : number;

                return new Project
                {
                    Id = GenerateProjectId(fullPath),
                    FullNumber = "P" + number,
                    ShortNumber = shortNumber,
                    Name = name,
                    Path = fullPath,
                    Year = year,
                    DriveLocation = driveLocation,
                    LastScanned = DateTime.UtcNow
                };
            }

            // Try old format (2024638.001 Name)
            var oldMatch = QDriveOldProjectPattern.Match(directoryName);
            if (oldMatch.Success)
            {
                var fullNumber = oldMatch.Groups["full_number"].Value + oldMatch.Groups["seq"].Value;
                var name = oldMatch.Groups["name"].Value?.Trim() ?? string.Empty;

                // Generate short number (last 4 digits)
                var shortNumber = fullNumber.Length >= 4 ? fullNumber.Substring(fullNumber.Length - 4) : fullNumber;

                return new Project
                {
                    Id = GenerateProjectId(fullPath),
                    FullNumber = fullNumber,
                    ShortNumber = shortNumber,
                    Name = name,
                    Path = fullPath,
                    Year = year,
                    DriveLocation = driveLocation,
                    LastScanned = DateTime.UtcNow
                };
            }
        }

        return null;
    }

    private static string GenerateProjectId(string path)
    {
        // Use path hash as stable ID
        return Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(path.ToLowerInvariant())
            )
        ).Substring(0, 16);
    }

    // Phase 2: profile-driven scan. Applies the profile's own regexes instead of the hardcoded
    // Q / P / L / Archive branches. Keeps the legacy ScanProjectsAsync overloads working because
    // they are still called from callers that haven't migrated yet — those route through the
    // drive-code-based TryParseProjectFolder path.
    public async Task<List<Project>> ScanProjectsAsync(ScanProfile profile, CancellationToken cancellationToken = default)
    {
        var projects = new List<Project>();

        if (profile.Mode != ScanProfileMode.ProjectMode) return projects;
        if (profile.ProjectPatterns is null) return projects;
        if (string.IsNullOrWhiteSpace(profile.RootPath)) return projects;
        if (!Directory.Exists(profile.RootPath)) return projects;

        Regex yearRegex;
        try
        {
            yearRegex = new Regex(profile.ProjectPatterns.YearDirRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
        catch (ArgumentException)
        {
            return projects;
        }

        var compiledPatterns = new List<(Regex regex, ProjectFolderPattern spec)>();
        foreach (var p in profile.ProjectPatterns.Patterns)
        {
            try
            {
                compiledPatterns.Add((new Regex(p.Regex, RegexOptions.Compiled | RegexOptions.IgnoreCase), p));
            }
            catch (ArgumentException)
            {
                // Skip invalid regex; log-and-continue.
            }
        }
        if (compiledPatterns.Count == 0) return projects;

        // Drive location stored on each Project. Prefer legacy drive code for backward-compat
        // with existing search filters; fall back to a short profile id for net-new profiles.
        var driveLocation = !string.IsNullOrEmpty(profile.LegacyDriveCode)
            ? profile.LegacyDriveCode
            : profile.Id.Length >= 8 ? profile.Id.Substring(0, 8) : profile.Id;

        var yearDirs = Directory.GetDirectories(profile.RootPath)
            .Where(d => yearRegex.IsMatch(Path.GetFileName(d)))
            .ToList();

        await Task.Run(() =>
        {
            foreach (var yearDir in yearDirs)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var yearMatch = yearRegex.Match(Path.GetFileName(yearDir));
                var year = yearMatch.Groups["year"].Value;
                if (year.Length == 2) year = "20" + year;

                try
                {
                    foreach (var projectDir in Directory.GetDirectories(yearDir))
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var dirName = Path.GetFileName(projectDir);
                        foreach (var (regex, spec) in compiledPatterns)
                        {
                            var m = regex.Match(dirName);
                            if (!m.Success) continue;

                            var fullNumber = m.Groups["full_number"].Value;
                            if (!string.IsNullOrEmpty(spec.FullNumberPrefix) && !fullNumber.StartsWith(spec.FullNumberPrefix))
                            {
                                fullNumber = spec.FullNumberPrefix + fullNumber;
                            }

                            var shortNumber = ExtractShortNumber(m, fullNumber, spec.ShortNumberStrategy);
                            var name = m.Groups["name"]?.Value?.Trim() ?? string.Empty;

                            projects.Add(new Project
                            {
                                Id = GenerateProjectId(projectDir),
                                FullNumber = fullNumber,
                                ShortNumber = shortNumber,
                                Name = name,
                                Path = projectDir,
                                Year = year,
                                DriveLocation = driveLocation,
                                LastScanned = DateTime.UtcNow
                            });
                            break;
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
        }, cancellationToken);

        return projects;
    }

    private static string ExtractShortNumber(Match match, string fullNumber, ShortNumberStrategy strategy)
    {
        switch (strategy)
        {
            case ShortNumberStrategy.Capture:
                var captured = match.Groups["short_number"]?.Value;
                if (!string.IsNullOrEmpty(captured)) return captured;
                return fullNumber;

            case ShortNumberStrategy.Last6Digits:
                {
                    var digitsOnly = fullNumber.Replace(".", "").Replace("-", "");
                    return digitsOnly.Length >= 6 ? digitsOnly.Substring(digitsOnly.Length - 6) : digitsOnly;
                }

            case ShortNumberStrategy.Last4Digits:
                {
                    var digitsOnly = fullNumber.Replace(".", "").Replace("-", "");
                    return digitsOnly.Length >= 4 ? digitsOnly.Substring(digitsOnly.Length - 4) : digitsOnly;
                }

            case ShortNumberStrategy.BeforeDecimal:
                {
                    var idx = fullNumber.IndexOf('.');
                    return idx > 0 ? fullNumber.Substring(0, idx) : fullNumber;
                }

            case ShortNumberStrategy.Full:
            default:
                return fullNumber;
        }
    }
}
