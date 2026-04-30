using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using LoadExtractor.Core.Models;

namespace LoadExtractor.Core.Services;

public class DesignLoadExtractor
{
    private const double LineGroupTolerance = 3.0;

    private record PdfWord(string Text, double Left, double Bottom, double Top, double Right);

    private class PdfLine
    {
        public double Y { get; set; }
        public List<PdfWord> Words { get; set; } = new();
        public string Text => string.Join(" ", Words.OrderBy(w => w.Left).Select(w => w.Text));
    }

    /// <summary>
    /// Extract all space component loads from a Space Design Load Summary PDF.
    /// </summary>
    public List<SpaceComponentLoads> Extract(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        return Extract(document, Enumerable.Range(1, document.NumberOfPages).ToList());
    }

    public List<SpaceComponentLoads> Extract(PdfDocument document, IReadOnlyList<int> pages)
    {
        var results = new List<SpaceComponentLoads>();

        var pageList = pages
            .Where(p => p >= 1 && p <= document.NumberOfPages)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        var allPageLines = new List<List<PdfLine>>();
        foreach (var pageIndex in pageList)
        {
            var page = document.GetPage(pageIndex);
            var words = page.GetWords()
                .Select(w => new PdfWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Bottom,
                                          w.BoundingBox.Top, w.BoundingBox.Right))
                .ToList();
            allPageLines.Add(GroupWordsIntoLines(words));
        }

        var systemSections = SplitBySystemHeader(allPageLines);
        foreach (var (systemName, sectionLines) in systemSections)
        {
            var spaceTables = FindComponentLoadTables(sectionLines, systemName);
            results.AddRange(spaceTables);
        }

        return results;
    }

    private List<PdfLine> GroupWordsIntoLines(List<PdfWord> words)
    {
        if (words.Count == 0) return new List<PdfLine>();

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

    /// <summary>
    /// Split pages into groups by system. Each system starts on a page with
    /// "Space Design Load Summary for XXXX" header.
    /// </summary>
    private List<(string SystemName, List<PdfLine> Lines)> SplitBySystemHeader(List<List<PdfLine>> allPageLines)
    {
        var sections = new List<(string, List<PdfLine>)>();
        string currentSystem = "";
        List<PdfLine>? currentLines = null;

        foreach (var pageLines in allPageLines)
        {
            string? newSystem = null;
            foreach (var line in pageLines)
            {
                var match = Regex.Match(line.Text,
                    @"(?:Space\s+)?Design\s+Load\s+Summary\s+for\s+(\S+)",
                    RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    newSystem = match.Groups[1].Value.Trim();
                    break;
                }
            }

            if (newSystem != null)
            {
                if (currentLines != null && !string.IsNullOrEmpty(currentSystem))
                {
                    sections.Add((currentSystem, currentLines));
                }
                currentSystem = newSystem;
                currentLines = new List<PdfLine>(pageLines);
            }
            else if (currentLines != null)
            {
                currentLines.AddRange(pageLines);
            }
        }

        if (currentLines != null && !string.IsNullOrEmpty(currentSystem))
        {
            sections.Add((currentSystem, currentLines));
        }

        return sections;
    }

    /// <summary>
    /// Find all TABLE 1.1.A instances within a system section and parse them.
    /// Table title: 'Component Loads For Space "SpaceName" In Zone "ZoneName"'
    /// </summary>
    private List<SpaceComponentLoads> FindComponentLoadTables(List<PdfLine> lines, string systemName)
    {
        var results = new List<SpaceComponentLoads>();

        for (int i = 0; i < lines.Count; i++)
        {
            var text = lines[i].Text;

            // Look for TABLE 1.1.A header or "Component Loads For Space"
            if (!text.Contains("Component Loads For Space", StringComparison.OrdinalIgnoreCase) &&
                !Regex.IsMatch(text, @"TABLE\s+\d+\.\d+\.A", RegexOptions.IgnoreCase))
                continue;

            // Extract space name and zone name from the title
            // Pattern: Component Loads For Space "SpaceName" In Zone "ZoneName"
            var titleMatch = Regex.Match(text,
                @"Component\s+Loads\s+For\s+Space\s+""([^""]+)""\s+In\s+Zone\s+""([^""]+)""",
                RegexOptions.IgnoreCase);

            if (!titleMatch.Success)
            {
                // Try without quotes — PdfPig may drop them
                titleMatch = Regex.Match(text,
                    @"Component\s+Loads\s+For\s+Space\s+(.+?)\s+In\s+Zone\s+(.+?)(?:\s*$)",
                    RegexOptions.IgnoreCase);
            }

            if (!titleMatch.Success) continue;

            var spaceName = titleMatch.Groups[1].Value.Trim().Trim('"');
            var zoneName = titleMatch.Groups[2].Value.Trim().Trim('"');

            // Parse the table starting from this line
            var loads = ParseComponentLoadTable(lines, i, spaceName, zoneName, systemName);
            if (loads != null)
            {
                results.Add(loads);
            }
        }

        return results;
    }

    /// <summary>
    /// Parse TABLE 1.1.A starting near lineIdx.
    /// The table has a DESIGN COOLING side (Details, Sensible, Latent) and
    /// a DESIGN HEATING side (Details, Sensible, Latent).
    /// </summary>
    private SpaceComponentLoads? ParseComponentLoadTable(List<PdfLine> lines, int startIdx,
        string spaceName, string zoneName, string systemName)
    {
        var loads = new SpaceComponentLoads
        {
            SpaceName = spaceName,
            ZoneName = zoneName,
            SystemName = systemName
        };

        // Find the column header line with "Sensible" and "Latent"
        // The table has: SPACE LOADS | Details | Sensible(BTU/hr) | Latent(BTU/hr) | Details | Sensible(BTU/hr) | Latent(BTU/hr)
        int headerLineIdx = -1;
        double coolDetailsX = 0, coolSensX = 0, coolLatentX = 0;
        double heatDetailsX = 0, heatSensX = 0, heatLatentX = 0;

        for (int i = startIdx + 1; i < Math.Min(startIdx + 15, lines.Count); i++)
        {
            var lineText = lines[i].Text;
            // Look for the line containing "Sensible" and "Latent" — this is the column header
            if (lineText.Contains("Sensible", StringComparison.OrdinalIgnoreCase) &&
                lineText.Contains("Latent", StringComparison.OrdinalIgnoreCase))
            {
                headerLineIdx = i;

                // Find X positions of column headers
                // There should be two "Sensible" words (cooling and heating) and two "Latent" words
                var sensWords = lines[i].Words
                    .Where(w => w.Text.Contains("Sensible", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(w => w.Left).ToList();
                var latentWords = lines[i].Words
                    .Where(w => w.Text.Contains("Latent", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(w => w.Left).ToList();

                if (sensWords.Count >= 2 && latentWords.Count >= 2)
                {
                    coolSensX = sensWords[0].Left;
                    coolLatentX = latentWords[0].Left;
                    heatSensX = sensWords[1].Left;
                    heatLatentX = latentWords[1].Left;
                }
                else if (sensWords.Count >= 1 && latentWords.Count >= 1)
                {
                    coolSensX = sensWords[0].Left;
                    coolLatentX = latentWords[0].Left;
                }

                break;
            }
        }

        if (headerLineIdx < 0)
        {
            Logger.Warn($"No header line found for space '{spaceName}' — skipping");
            return null;
        }

        Logger.Info($"TABLE 1.1.A for '{spaceName}': headerLine={headerLineIdx}, " +
                    $"coolSensX={coolSensX:F1}, coolLatentX={coolLatentX:F1}, heatSensX={heatSensX:F1}, heatLatentX={heatLatentX:F1}");

        // Dump all header lines for debugging
        for (int i = startIdx; i <= Math.Min(headerLineIdx + 2, lines.Count - 1); i++)
        {
            Logger.Info($"  HeaderScan line[{i}]: '{lines[i].Text}'");
            foreach (var w in lines[i].Words.OrderBy(ww => ww.Left))
                Logger.Info($"    word '{w.Text}' at X={w.Left:F1}..{w.Right:F1}");
        }

        // Find "Details" columns — scan a wide range around the header
        // The word "Details" may appear on headerLineIdx itself or on a separate sub-header line
        for (int i = Math.Max(startIdx, headerLineIdx - 4); i <= Math.Min(headerLineIdx + 2, lines.Count - 1); i++)
        {
            var detailsWords = lines[i].Words
                .Where(w => w.Text.Equals("Details", StringComparison.OrdinalIgnoreCase))
                .OrderBy(w => w.Left).ToList();

            if (detailsWords.Count >= 2)
            {
                coolDetailsX = detailsWords[0].Left;
                heatDetailsX = detailsWords[1].Left;
                Logger.Info($"  Found 2 Details words on line[{i}]: cool={coolDetailsX:F1}, heat={heatDetailsX:F1}");
                break;
            }
            else if (detailsWords.Count == 1)
            {
                if (coolDetailsX == 0)
                    coolDetailsX = detailsWords[0].Left;
                else if (heatDetailsX == 0)
                    heatDetailsX = detailsWords[0].Left;
                Logger.Info($"  Found 1 Details word on line[{i}] at X={detailsWords[0].Left:F1}");
            }
        }

        // If Details columns still not found, infer from Sensible positions
        // Details column is typically ~70px to the left of Sensible
        if (coolDetailsX == 0 && coolSensX > 0)
        {
            coolDetailsX = coolSensX - 70;
            Logger.Warn($"  Inferred coolDetailsX={coolDetailsX:F1} from coolSensX={coolSensX:F1}");
        }
        if (heatDetailsX == 0 && heatSensX > 0)
        {
            heatDetailsX = heatSensX - 70;
            Logger.Warn($"  Inferred heatDetailsX={heatDetailsX:F1} from heatSensX={heatSensX:F1}");
        }

        Logger.Info($"  Final Details columns: coolDetailsX={coolDetailsX:F1}, heatDetailsX={heatDetailsX:F1}");

        // Determine the midpoint between cooling and heating columns
        double midX = 0;
        if (coolLatentX > 0 && heatDetailsX > 0)
            midX = (coolLatentX + heatDetailsX) / 2;
        else if (coolLatentX > 0 && heatSensX > 0)
            midX = (coolLatentX + heatSensX) / 2;
        else if (coolSensX > 0)
            midX = coolSensX + 200; // fallback

        // Parse data rows
        var rowMap = BuildRowMap(loads);

        for (int i = headerLineIdx + 1; i < lines.Count; i++)
        {
            var lineText = lines[i].Text.Trim();

            // Stop at next TABLE header or end of section
            if (lineText.Contains("TABLE", StringComparison.OrdinalIgnoreCase) &&
                !lineText.Contains("Total Zone Loads", StringComparison.OrdinalIgnoreCase))
                break;
            if (lineText.Contains("Envelope Loads", StringComparison.OrdinalIgnoreCase))
                break;

            if (string.IsNullOrWhiteSpace(lineText)) continue;

            // Identify which row this is by matching the row name
            var matchedRow = MatchRowName(lineText, rowMap);
            if (matchedRow == null) continue;

            // Extract values from this line
            ParseRowValues(lines[i], matchedRow, coolDetailsX, coolSensX, coolLatentX,
                           heatDetailsX, heatSensX, heatLatentX, midX);
        }

        return loads;
    }

    private Dictionary<string, ComponentLoadRow> BuildRowMap(SpaceComponentLoads loads)
    {
        return new Dictionary<string, ComponentLoadRow>(StringComparer.OrdinalIgnoreCase)
        {
            ["window & skylight"] = loads.WindowSkylightSolar,
            ["window & skylight solar"] = loads.WindowSkylightSolar,
            ["wall transmission"] = loads.WallTransmission,
            ["wall trans"] = loads.WallTransmission,
            ["roof transmission"] = loads.RoofTransmission,
            ["roof trans"] = loads.RoofTransmission,
            ["window transmission"] = loads.WindowTransmission,
            ["window trans"] = loads.WindowTransmission,
            ["skylight transmission"] = loads.SkylightTransmission,
            ["skylight trans"] = loads.SkylightTransmission,
            ["door loads"] = loads.DoorLoads,
            ["door"] = loads.DoorLoads,
            ["floor transmission"] = loads.FloorTransmission,
            ["floor trans"] = loads.FloorTransmission,
            ["partitions"] = loads.Partitions,
            ["partition"] = loads.Partitions,
            ["ceiling"] = loads.Ceiling,
            ["overhead lighting"] = loads.OverheadLighting,
            ["overhead light"] = loads.OverheadLighting,
            ["task lighting"] = loads.TaskLighting,
            ["task light"] = loads.TaskLighting,
            ["electric equipment"] = loads.ElectricEquipment,
            ["electric equip"] = loads.ElectricEquipment,
            ["people"] = loads.People,
            ["infiltration"] = loads.Infiltration,
            ["miscellaneous"] = loads.Miscellaneous,
            ["misc"] = loads.Miscellaneous,
            ["safety factor"] = loads.SafetyFactor,
            ["safety"] = loads.SafetyFactor,
            ["total zone loads"] = loads.TotalZoneLoads,
            [">> total zone loads"] = loads.TotalZoneLoads,
            ["total zone"] = loads.TotalZoneLoads,
        };
    }

    private ComponentLoadRow? MatchRowName(string lineText, Dictionary<string, ComponentLoadRow> rowMap)
    {
        var lower = lineText.ToLowerInvariant();
        // Try longest matches first to avoid false positives
        foreach (var kvp in rowMap.OrderByDescending(k => k.Key.Length))
        {
            if (lower.Contains(kvp.Key))
            {
                // Make sure it's at the start of the line (row name is leftmost)
                var idx = lower.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase);
                if (idx < 30) // Row name should be near the start
                    return kvp.Value;
            }
        }
        return null;
    }

    private void ParseRowValues(PdfLine line, ComponentLoadRow row,
        double coolDetailsX, double coolSensX, double coolLatentX,
        double heatDetailsX, double heatSensX, double heatLatentX, double midX)
    {
        var allWords = line.Words.OrderBy(w => w.Left).ToList();

        // Cooling side — Details may be multi-word like "75 ft²" or "1770 W"
        row.CoolingDetails = FindDetailsNearX(allWords, coolDetailsX, coolSensX > 0 ? coolSensX : midX);
        row.CoolingSensible = FindNumericNearX(allWords, coolSensX, coolLatentX > 0 ? coolLatentX : midX);
        row.CoolingLatent = FindNumericNearX(allWords, coolLatentX, midX);

        // Heating side
        row.HeatingDetails = FindDetailsNearX(allWords, heatDetailsX, heatSensX > 0 ? heatSensX : heatLatentX);
        row.HeatingSensible = FindNumericNearX(allWords, heatSensX, heatLatentX > 0 ? heatLatentX : heatSensX + 100);
        row.HeatingLatent = FindNumericNearX(allWords, heatLatentX, heatLatentX + 100);

        Logger.Info($"  Row '{row.RowName}': CoolDet='{row.CoolingDetails}' CoolSens={row.CoolingSensible} " +
                    $"CoolLat={row.CoolingLatent} HeatDet='{row.HeatingDetails}' HeatSens={row.HeatingSensible} HeatLat={row.HeatingLatent}");
    }

    /// <summary>
    /// Find the Details value near targetX. Details can be multi-word: "75 ft²", "1770 W", "5% / 5%", "0".
    /// Concatenate all words in the column range.
    /// </summary>
    private string FindDetailsNearX(List<PdfWord> words, double targetX, double maxX)
    {
        if (targetX <= 0) return "";

        // Collect ALL words in the Details column range
        var inRange = words
            .Where(w => w.Left >= targetX - 30 && w.Right < maxX + 5)
            .OrderBy(w => w.Left)
            .ToList();

        if (inRange.Count == 0) return "";

        // Join them — handles "75 ft²", "1770 W", "5% / 5%", or just "0"
        var combined = string.Join(" ", inRange.Select(w => w.Text)).Trim();

        // Clean up dashes that mean "no value"
        if (combined == "-" || combined == "–") return "";

        return combined;
    }

    private double FindNumericNearX(List<PdfWord> words, double targetX, double maxX)
    {
        if (targetX <= 0) return 0;

        // Look for numeric words in the column range
        foreach (var w in words.Where(w => w.Left >= targetX - 25 && w.Left < maxX)
                               .OrderBy(w => Math.Abs(w.Left - targetX)))
        {
            var cleanText = w.Text.Replace(",", "").Trim('-', '–');
            if (double.TryParse(cleanText, out double val))
                return val;
        }

        return 0;
    }
}
