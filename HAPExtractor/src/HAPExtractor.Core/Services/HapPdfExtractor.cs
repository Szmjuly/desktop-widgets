using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using HAPExtractor.Core.Models;

namespace HAPExtractor.Core.Services;

public class HapPdfExtractor
{
    private const double LineGroupTolerance = 3.0;

    /// <summary>
    /// A single word with its bounding box coordinates.
    /// PDF coordinate system: Y increases upward from bottom-left.
    /// </summary>
    private record PdfWord(string Text, double Left, double Bottom, double Top, double Right);

    /// <summary>
    /// A reconstructed text line from grouped words.
    /// </summary>
    private class PdfLine
    {
        public double Y { get; set; }
        public List<PdfWord> Words { get; set; } = new();
        public string Text => string.Join(" ", Words.OrderBy(w => w.Left).Select(w => w.Text));

        public string GetTextInRange(double xMin, double xMax)
        {
            var inRange = Words.Where(w => w.Left >= xMin - 5 && w.Left < xMax)
                                .OrderBy(w => w.Left)
                                .Select(w => w.Text);
            return string.Join(" ", inRange);
        }
    }

    /// <summary>
    /// Extract project data from a HAP Zone Sizing Summary PDF.
    /// </summary>
    public HapProject Extract(string pdfPath)
    {
        var project = new HapProject { SourceFile = pdfPath };

        using var document = PdfDocument.Open(pdfPath);

        // Extract all words with positions from all pages
        var allPageLines = new List<List<PdfLine>>();
        for (int i = 1; i <= document.NumberOfPages; i++)
        {
            var page = document.GetPage(i);
            var words = page.GetWords()
                .Select(w => new PdfWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Bottom,
                                          w.BoundingBox.Top, w.BoundingBox.Right))
                .ToList();

            var lines = GroupWordsIntoLines(words);
            allPageLines.Add(lines);
        }

        // Extract project name from first page
        if (allPageLines.Count > 0)
        {
            project.ProjectName = ExtractProjectName(allPageLines[0]);
        }

        // Split pages into air system sections
        var systemSections = SplitIntoAirSystemSections(allPageLines);

        foreach (var section in systemSections)
        {
            var airSystem = ParseAirSystemSection(section);
            if (!string.IsNullOrEmpty(airSystem.Name))
            {
                project.AirSystems.Add(airSystem);
            }
        }

        return project;
    }

    /// <summary>
    /// Group words into lines based on their Y coordinate.
    /// Words within LineGroupTolerance of each other vertically are on the same line.
    /// </summary>
    private List<PdfLine> GroupWordsIntoLines(List<PdfWord> words)
    {
        if (words.Count == 0) return new List<PdfLine>();

        // Sort by Y descending (top of page first), then X ascending
        var sorted = words.OrderByDescending(w => w.Bottom).ThenBy(w => w.Left).ToList();

        var lines = new List<PdfLine>();
        var currentLine = new PdfLine { Y = sorted[0].Bottom };
        currentLine.Words.Add(sorted[0]);

        for (int i = 1; i < sorted.Count; i++)
        {
            if (Math.Abs(sorted[i].Bottom - currentLine.Y) <= LineGroupTolerance)
            {
                currentLine.Words.Add(sorted[i]);
            }
            else
            {
                lines.Add(currentLine);
                currentLine = new PdfLine { Y = sorted[i].Bottom };
                currentLine.Words.Add(sorted[i]);
            }
        }
        lines.Add(currentLine);

        return lines;
    }

    private string ExtractProjectName(List<PdfLine> firstPageLines)
    {
        foreach (var line in firstPageLines)
        {
            var text = line.Text;
            if (text.Contains("Project Name", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Project Nam", StringComparison.OrdinalIgnoreCase))
            {
                // The project name follows "Project Name" on the same line
                var match = Regex.Match(text, @"Project\s+Nam[e]?\s*[:\.\s]*(.+?)(?:\s+Prepared|\s+\d{1,2}/\d{1,2}|\s*$)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var name = match.Groups[1].Value.Trim();
                    // Clean trailing dots/spaces
                    name = name.TrimEnd('.', ' ');
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }
        }
        return "Unknown Project";
    }

    /// <summary>
    /// Split pages into groups, one per air system.
    /// Each air system starts on a page containing "Zone Sizing Summary for XXX".
    /// </summary>
    private List<List<PdfLine>> SplitIntoAirSystemSections(List<List<PdfLine>> allPageLines)
    {
        var sections = new List<List<PdfLine>>();
        List<PdfLine>? currentSection = null;

        foreach (var pageLines in allPageLines)
        {
            // Check if this page has a "Zone Sizing Summary for" header
            bool isNewSystem = false;
            foreach (var line in pageLines)
            {
                if (line.Text.Contains("Zone Sizing Summary for", StringComparison.OrdinalIgnoreCase))
                {
                    isNewSystem = true;
                    break;
                }
            }

            if (isNewSystem)
            {
                if (currentSection != null)
                {
                    sections.Add(currentSection);
                }
                currentSection = new List<PdfLine>(pageLines);
            }
            else if (currentSection != null)
            {
                // Continuation page for current system
                currentSection.AddRange(pageLines);
            }
        }

        if (currentSection != null)
        {
            sections.Add(currentSection);
        }

        return sections;
    }

    private AirSystem ParseAirSystemSection(List<PdfLine> lines)
    {
        var airSystem = new AirSystem();

        // Extract system name from "Zone Sizing Summary for XXXX"
        foreach (var line in lines)
        {
            var match = Regex.Match(line.Text, @"Zone\s+Sizing\s+Summary\s+for\s+(\S+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                airSystem.Name = match.Groups[1].Value.Trim();
                break;
            }
        }

        // Extract floor area from "Floor Area ... XXXX.X ft²"
        foreach (var line in lines)
        {
            var match = Regex.Match(line.Text, @"Floor\s+Area\s*[\.:\s]+([\d,]+\.?\d*)\s*(?:ft|$)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (double.TryParse(match.Groups[1].Value.Replace(",", ""), out double area))
                {
                    airSystem.FloorArea = area;
                }
                break;
            }
        }

        // Find the "Space Loads and Airflows" table and extract data
        airSystem.Spaces = ExtractSpaceLoadsTable(lines);

        return airSystem;
    }

    /// <summary>
    /// Find and parse the "Space Loads and Airflows" table.
    /// Table columns (from PDF):
    ///   Zone Name / Space Name | Mult. | Cooling Sensible (MBH) | Time of Peak Sensible Load | Air Flow (CFM) | Heating Load (MBH) | Floor Area (ft²) | Space CFM/ft²
    /// </summary>
    private List<SpaceLoad> ExtractSpaceLoadsTable(List<PdfLine> lines)
    {
        var spaces = new List<SpaceLoad>();

        // Step 1: Find the "Space Loads and Airflows" section header
        int tableStartIdx = -1;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Text.Contains("Space Loads and Airflows", StringComparison.OrdinalIgnoreCase))
            {
                tableStartIdx = i;
                break;
            }
        }

        if (tableStartIdx < 0) return spaces;

        // Step 2: Find the column header line(s) — look for "Space Name" and "Mult"
        // The headers may span 2-3 lines. We need to find where "Mult" appears to get column X positions.
        int headerLineIdx = -1;
        double multX = 0, coolSensX = 0, timeX = 0, airFlowX = 0, heatLoadX = 0, floorAreaX = 0, cfmFtX = 0;

        for (int i = tableStartIdx + 1; i < Math.Min(tableStartIdx + 8, lines.Count); i++)
        {
            var line = lines[i];
            foreach (var word in line.Words)
            {
                if (word.Text.Equals("Mult.", StringComparison.OrdinalIgnoreCase) ||
                    word.Text.Equals("Mult", StringComparison.OrdinalIgnoreCase))
                {
                    headerLineIdx = i;
                    multX = word.Left;
                    break;
                }
            }
            if (headerLineIdx >= 0) break;
        }

        if (headerLineIdx < 0) return spaces;

        // Step 3: Determine column X positions from header line and nearby header lines
        // Scan header lines (headerLineIdx - 2 to headerLineIdx + 1) for column keywords
        for (int i = Math.Max(tableStartIdx + 1, headerLineIdx - 3); i <= Math.Min(headerLineIdx + 1, lines.Count - 1); i++)
        {
            foreach (var word in lines[i].Words)
            {
                var t = word.Text;
                if (t.Equals("Cooling", StringComparison.OrdinalIgnoreCase) && coolSensX == 0)
                    coolSensX = word.Left;
                else if (t.Equals("Sensible", StringComparison.OrdinalIgnoreCase) && coolSensX == 0)
                    coolSensX = word.Left;
                else if ((t.Equals("Time", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Peak", StringComparison.OrdinalIgnoreCase)) && timeX == 0)
                {
                    // "Time of" or "Peak Sensible" header
                    if (word.Left > multX + 20) // Must be to the right of Mult
                        timeX = word.Left;
                }
                else if (t.Equals("Air", StringComparison.OrdinalIgnoreCase) && airFlowX == 0)
                {
                    if (word.Left > multX + 40)
                        airFlowX = word.Left;
                }
                else if (t.Equals("Flow", StringComparison.OrdinalIgnoreCase) && airFlowX == 0)
                {
                    if (word.Left > multX + 40)
                        airFlowX = word.Left;
                }
                else if (t.Equals("Heating", StringComparison.OrdinalIgnoreCase) && heatLoadX == 0)
                {
                    if (word.Left > multX + 60)
                        heatLoadX = word.Left;
                }
                else if (t.Equals("Floor", StringComparison.OrdinalIgnoreCase) && floorAreaX == 0)
                {
                    if (word.Left > multX + 80)
                        floorAreaX = word.Left;
                }
                else if ((t.Equals("Space", StringComparison.OrdinalIgnoreCase) || t.StartsWith("CFM/ft", StringComparison.OrdinalIgnoreCase)) && cfmFtX == 0)
                {
                    if (word.Left > multX + 100)
                        cfmFtX = word.Left;
                }
            }
        }

        // Step 4: Find column boundaries by sorting the X positions we found
        // Columns: Name(0..multX), Mult(multX..), CoolSens(..), Time(..), AirFlow(..), HeatLoad(..), FloorArea(..), CFM/ft²(..)
        var colBoundaries = new List<double> { multX };
        if (coolSensX > 0) colBoundaries.Add(coolSensX);
        if (timeX > 0) colBoundaries.Add(timeX);
        if (airFlowX > 0) colBoundaries.Add(airFlowX);
        if (heatLoadX > 0) colBoundaries.Add(heatLoadX);
        if (floorAreaX > 0) colBoundaries.Add(floorAreaX);
        if (cfmFtX > 0) colBoundaries.Add(cfmFtX);
        colBoundaries.Sort();

        // Step 5: Parse data rows after the header
        // Data rows start after the last header line and contain numeric data
        // "Zone 1" lines are sub-headers (skip them)
        for (int i = headerLineIdx + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            var text = line.Text.Trim();

            // Stop if we hit a new section header
            if (text.Contains("Zone Sizing Summary", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Zone Terminal Sizing", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Zone Peak Sensible", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Hourly Analysis Program", StringComparison.OrdinalIgnoreCase))
                break;

            // Skip empty lines, zone sub-headers, and header-like lines
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (Regex.IsMatch(text, @"^Zone\s+\d+\s*$", RegexOptions.IgnoreCase)) continue;
            if (text.Contains("Space Name", StringComparison.OrdinalIgnoreCase)) continue;
            if (text.Contains("Mult.", StringComparison.OrdinalIgnoreCase)) continue;

            // A valid data row should have at least some words to the left of multX (the space name)
            // and at least some numeric words to the right
            var nameWords = line.Words.Where(w => w.Right <= multX + 5).OrderBy(w => w.Left).ToList();
            var dataWords = line.Words.Where(w => w.Left >= multX - 5).OrderBy(w => w.Left).ToList();

            if (nameWords.Count == 0 || dataWords.Count < 2) continue;

            var spaceName = string.Join(" ", nameWords.Select(w => w.Text));

            // Skip non-data rows
            if (IsHeaderOrMetadata(spaceName)) continue;

            // Two-pass approach for time-of-peak detection:
            // Pass 1: Find month abbreviation and its adjacent time digit (e.g., "Nov" + "1500")
            //         Mark the time digit word so we exclude it from numeric values.
            string timeOfPeak = "";
            var timeDigitWords = new HashSet<PdfWord>();

            var sortedDataWords = dataWords.OrderBy(w => w.Left).ToList();
            for (int wi = 0; wi < sortedDataWords.Count; wi++)
            {
                var w = sortedDataWords[wi];
                if (Regex.IsMatch(w.Text, @"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)$", RegexOptions.IgnoreCase))
                {
                    timeOfPeak = w.Text;
                    // The next word should be the time digit (e.g., "1500")
                    if (wi + 1 < sortedDataWords.Count)
                    {
                        var next = sortedDataWords[wi + 1];
                        if (Regex.IsMatch(next.Text, @"^\d{3,4}$"))
                        {
                            timeOfPeak += " " + next.Text;
                            timeDigitWords.Add(next);
                        }
                    }
                    break;
                }
            }

            // Pass 2: Collect numeric values, excluding time digit words and month words
            var numericValues = new List<(double value, double x, string raw)>();
            foreach (var w in sortedDataWords)
            {
                // Skip month abbreviations
                if (Regex.IsMatch(w.Text, @"^(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)$", RegexOptions.IgnoreCase))
                    continue;
                // Skip time digit words
                if (timeDigitWords.Contains(w))
                    continue;

                if (double.TryParse(w.Text.Replace(",", ""), out double val))
                {
                    numericValues.Add((val, w.Left, w.Text));
                }
            }

            // Expected numeric order: Mult, CoolingSensible, AirFlow, HeatingLoad, FloorArea, SpaceCFM/ft²
            // (Time of Peak is already extracted separately)
            if (numericValues.Count >= 6)
            {
                var space = new SpaceLoad
                {
                    SpaceName = spaceName,
                    Multiplier = (int)numericValues[0].value,
                    CoolingSensible = numericValues[1].value,
                    TimeOfPeakSensible = timeOfPeak,
                    AirFlow = numericValues[2].value,
                    HeatingLoad = numericValues[3].value,
                    FloorArea = numericValues[4].value,
                    SpaceCfmPerSqFt = numericValues[5].value,
                };
                spaces.Add(space);
            }
            else if (numericValues.Count >= 4)
            {
                // Partial row — try to extract what we can
                var space = new SpaceLoad
                {
                    SpaceName = spaceName,
                    Multiplier = (int)numericValues[0].value,
                    CoolingSensible = numericValues[1].value,
                    TimeOfPeakSensible = timeOfPeak,
                };
                if (numericValues.Count >= 5)
                {
                    space.AirFlow = numericValues[2].value;
                    space.HeatingLoad = numericValues[3].value;
                    space.FloorArea = numericValues[4].value;
                }
                if (numericValues.Count >= 6)
                    space.SpaceCfmPerSqFt = numericValues[5].value;
                spaces.Add(space);
            }
        }

        return spaces;
    }

    private bool IsHeaderOrMetadata(string text)
    {
        var lower = text.ToLowerInvariant().Trim();
        return lower.Contains("space name") ||
               lower.Contains("zone name") ||
               lower.Contains("air system") ||
               lower.Contains("equipment class") ||
               lower.Contains("zone sizing") ||
               lower.Contains("sizing calculation") ||
               lower.Contains("calculation months") ||
               lower.Contains("sizing data") ||
               lower.Contains("zone cfm") ||
               lower.Contains("space cfm") ||
               lower.Contains("project name") ||
               lower.Contains("prepared by") ||
               lower.Contains("number of zones") ||
               lower.Contains("floor area") ||
               lower.Contains("location") ||
               lower.Contains("totals") ||
               lower.Contains("grand total") ||
               lower.Contains("cooling") ||
               lower.Contains("sensible") ||
               lower.Contains("heating") ||
               lower.Contains("air flow") ||
               lower.StartsWith("zone ") && Regex.IsMatch(lower, @"^zone\s+\d+$");
    }
}
