using System.Text.RegularExpressions;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;

namespace DesktopHub.Infrastructure.Scanning;

/// <summary>
/// Scans Q: drive for project folders using regex patterns
/// </summary>
public class ProjectScanner : IProjectScanner
{
    // Old format: 2024638.001 Project Name
    private static readonly Regex OldProjectPattern = new(
        @"^(?<full_number>\d{4})(?<seq>\d{3}\.?\d*)(\s+)?(?<name>.+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // New format: P250784.00 - Project Name
    private static readonly Regex NewProjectPattern = new(
        @"^P(?<number>\d{6}\.?\d*)(\s*-\s*)?(?<name>.+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // Year directory pattern: _Proj-24, _Proj-2024, etc.
    private static readonly Regex YearDirPattern = new(
        @"^_Proj-(?<year>\d{2,4})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public async Task<List<Project>> ScanProjectsAsync(string drivePath, CancellationToken cancellationToken = default)
    {
        var projects = new List<Project>();

        if (!Directory.Exists(drivePath))
        {
            throw new DirectoryNotFoundException($"Q: drive not found at {drivePath}");
        }

        // Find all year directories (_Proj-24, _Proj-2024, etc.)
        var yearDirectories = Directory.GetDirectories(drivePath)
            .Where(dir => YearDirPattern.IsMatch(Path.GetFileName(dir)))
            .ToList();

        foreach (var yearDir in yearDirectories)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var yearProjects = await ScanYearDirectoryAsync(yearDir, cancellationToken);
            projects.AddRange(yearProjects);
        }

        return projects;
    }

    public async Task<List<Project>> ScanYearDirectoryAsync(string yearDirectoryPath, CancellationToken cancellationToken = default)
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
                    var project = TryParseProjectFolder(dirName, dir, year);
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
        // Try new format first (P250784.00 - Name)
        var newMatch = NewProjectPattern.Match(directoryName);
        if (newMatch.Success)
        {
            var number = newMatch.Groups["number"].Value;
            var name = newMatch.Groups["name"].Value.Trim();

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
                LastScanned = DateTime.UtcNow
            };
        }

        // Try old format (2024638.001 Name)
        var oldMatch = OldProjectPattern.Match(directoryName);
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
                LastScanned = DateTime.UtcNow
            };
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
}
