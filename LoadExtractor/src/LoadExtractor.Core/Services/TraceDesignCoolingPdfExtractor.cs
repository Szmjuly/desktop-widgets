using System.Globalization;
using System.Text.RegularExpressions;
using LoadExtractor.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LoadExtractor.Core.Services;

/// <summary>
/// Parses TRACE 700 "Design Cooling Load Summary" pages (e.g. Room - 001 NAME, System - AHU-xx).
/// </summary>
public class TraceDesignCoolingPdfExtractor
{
    private const double LineGroupTolerance = 3.0;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly Regex TripleBtu = new(
        @"^\s*(\d{1,3}(?:,\d{3})+)\s+(\d{1,3}(?:,\d{3})+)\s+(\d{1,3}(?:,\d{3})+)\s*$",
        RegexOptions.Compiled);

    private record PdfWord(string Text, double Left, double Bottom, double Top, double Right);

    private class PdfLine
    {
        public double Y { get; init; }
        public List<PdfWord> Words { get; } = new();
        public string Text => string.Join(" ", Words.OrderBy(w => w.Left).Select(w => w.Text));
        public string NormalizedText => Text.Replace("\u00b2", "2", StringComparison.Ordinal);
    }

    public List<TraceDesignCoolingRoomExtract> Extract(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        return Extract(document, Enumerable.Range(1, document.NumberOfPages).ToList());
    }

    public List<TraceDesignCoolingRoomExtract> Extract(PdfDocument document, IReadOnlyList<int> pages)
    {
        var results = new List<TraceDesignCoolingRoomExtract>();
        foreach (var pageIndex in pages.Where(p => p >= 1 && p <= document.NumberOfPages).Distinct().OrderBy(p => p))
        {
            var page = document.GetPage(pageIndex);
            var lines = GetPageLines(page);
            if (!LooksLikeDesignCoolingPage(lines))
                continue;
            var row = ParsePage(lines, pageIndex, page.Width);
            if (row != null)
                results.Add(row);
        }

        return results;
    }

    private static bool LooksLikeDesignCoolingPage(List<PdfLine> lines)
    {
        var flat = string.Join("\n", lines.Select(l => l.Text));
        return flat.Contains("Design Cooling Load Summary", StringComparison.OrdinalIgnoreCase) &&
               flat.Contains("Room -", StringComparison.OrdinalIgnoreCase);
    }

    private static TraceDesignCoolingRoomExtract? ParsePage(List<PdfLine> lines, int pageIndex, double pageWidth)
    {
        var row = new TraceDesignCoolingRoomExtract { SourcePage = pageIndex };

        ParseHeader(lines, row);
        ParseRoomZoneSystem(lines, row);
        ParseFooterProject(lines, row);
        ParseReportTotalCoolingLoads(lines, row);
        ParseCoilBlock(lines, row, pageWidth);
        ParseEngineeringChecks(lines, row, pageWidth);

        if (string.IsNullOrWhiteSpace(row.RoomNumber) && string.IsNullOrWhiteSpace(row.RoomName))
            return null;

        return row;
    }

    private static void ParseHeader(List<PdfLine> lines, TraceDesignCoolingRoomExtract row)
    {
        var titleIdx = lines.FindIndex(l =>
            l.Text.Contains("Design Cooling Load Summary", StringComparison.OrdinalIgnoreCase));
        if (titleIdx < 0)
            return;

        if (titleIdx + 1 < lines.Count)
        {
            var byText = lines[titleIdx + 1].Text.Trim();
            if (byText.StartsWith("By ", StringComparison.OrdinalIgnoreCase))
                row.CalcBy = byText[3..].Trim();
        }

        for (var i = titleIdx + 2; i < Math.Min(titleIdx + 6, lines.Count); i++)
        {
            var t = lines[i].Text.Trim();
            if (t.Length < 2) continue;
            if (t.Contains("Solar", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Glass", StringComparison.OrdinalIgnoreCase) ||
                t.Contains("Wall Transmission", StringComparison.OrdinalIgnoreCase))
                break;
            row.HeaderProjectTitle = t;
            break;
        }
    }

    private static void ParseRoomZoneSystem(List<PdfLine> lines, TraceDesignCoolingRoomExtract row)
    {
        foreach (var line in lines)
        {
            var t = line.Text;
            if (t.Contains("Room -", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(t, @"Room\s*-\s*(\S+)\s+(.+)$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    row.RoomNumber = m.Groups[1].Value.Trim();
                    row.RoomName = m.Groups[2].Value.Trim();
                }
            }
            else if (t.Contains("Zone -", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(t, @"Zone\s*-\s*(.+)$", RegexOptions.IgnoreCase);
                if (m.Success)
                    row.ZoneText = m.Groups[1].Value.Trim();
            }
            else if (t.Contains("System -", StringComparison.OrdinalIgnoreCase))
            {
                var m = Regex.Match(t, @"System\s*-\s*(\S+)", RegexOptions.IgnoreCase);
                if (m.Success)
                    row.SystemName = m.Groups[1].Value.Trim();
            }
        }
    }

    private static void ParseFooterProject(List<PdfLine> lines, TraceDesignCoolingRoomExtract row)
    {
        foreach (var line in lines)
        {
            if (!line.Text.Contains("Project Name", StringComparison.OrdinalIgnoreCase))
                continue;
            var m = Regex.Match(line.Text, @"Project\s+Name\s*:\s*(.+?)(?:\s+Dataset|\s+TRACE|$)",
                RegexOptions.IgnoreCase);
            if (m.Success)
            {
                row.FooterProjectName = m.Groups[1].Value.Trim();
                return;
            }

            var idx = line.Text.IndexOf(":", StringComparison.Ordinal);
            if (idx >= 0)
                row.FooterProjectName = line.Text[(idx + 1)..].Trim();
            return;
        }
    }

    private static void ParseReportTotalCoolingLoads(List<PdfLine> lines, TraceDesignCoolingRoomExtract row)
    {
        var coilIdx = lines.FindIndex(l =>
            l.Text.Contains("COOLING", StringComparison.OrdinalIgnoreCase) &&
            l.Text.Contains("COIL", StringComparison.OrdinalIgnoreCase));

        var limit = coilIdx >= 0 ? coilIdx : lines.Count;
        double bestTot = 0;

        for (var i = 0; i < limit; i++)
        {
            var m = TripleBtu.Match(lines[i].Text.Trim());
            if (!m.Success)
                continue;

            var s = ParseCommaDouble(m.Groups[1].Value);
            var lat = ParseCommaDouble(m.Groups[2].Value);
            var tot = ParseCommaDouble(m.Groups[3].Value);
            if (!s.HasValue || !lat.HasValue || !tot.HasValue)
                continue;
            if (tot.Value <= bestTot || tot.Value < 8_000)
                continue;

            bestTot = tot.Value;
            row.ReportSensibleBtuH = s;
            row.ReportLatentBtuH = lat;
            row.ReportTotalBtuH = tot;
        }
    }

    private static void ParseCoilBlock(List<PdfLine> lines, TraceDesignCoolingRoomExtract row, double pageWidth)
    {
        var end = lines.FindIndex(l =>
            l.Text.Contains("General Engineering Checks", StringComparison.OrdinalIgnoreCase));
        if (end < 0)
            return;

        // Label-driven: the load breakdown table (e.g. People row "10,000" Btu/h) often sits in the same
        // horizontal band as "COOLING COIL SELECTION"; scraping every right-column number pulls those Btu/h
        // totals and mis-assigns them as MBh. Read only the coil selection rows.
        for (var i = 0; i < end; i++)
        {
            var t = lines[i].Text;
            if (t.Contains("Coil Sensible Load", StringComparison.OrdinalIgnoreCase))
            {
                var v = ExtractRightColumnNumber(lines[i], pageWidth);
                if (v.HasValue)
                    row.CoilSensibleMbh = v;
            }
            else if (t.Contains("Coil Total Load", StringComparison.OrdinalIgnoreCase))
            {
                var v = ExtractRightColumnNumber(lines[i], pageWidth);
                if (v.HasValue)
                    row.CoilTotalMbh = v;
            }
            else if (t.Contains("Total Cooling Airflow", StringComparison.OrdinalIgnoreCase))
            {
                var v = ExtractRightColumnNumber(lines[i], pageWidth);
                if (v.HasValue)
                    row.TotalCoolingAirflowCfm = v;
            }
        }

        if (row.CoilSensibleMbh is null || row.CoilTotalMbh is null || row.TotalCoolingAirflowCfm is null)
            ParseCoilBlockHeuristic(lines, row, pageWidth, end);
    }

    /// <summary>
    /// Legacy heuristic when labels are missing or split across lines in the PDF.
    /// </summary>
    private static void ParseCoilBlockHeuristic(
        List<PdfLine> lines,
        TraceDesignCoolingRoomExtract row,
        double pageWidth,
        int endGeneralEng)
    {
        var start = lines.FindIndex(l =>
            l.Text.Contains("Coil Leaving Humidity Ratio", StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            start = lines.FindIndex(l =>
                l.Text.Contains("Coil Sensible Load", StringComparison.OrdinalIgnoreCase));
        }

        if (start < 0 || endGeneralEng <= start)
            return;

        var nums = new List<double>();
        for (var i = start + 1; i < endGeneralEng; i++)
        {
            var v = ExtractRightColumnNumber(lines[i], pageWidth);
            if (v.HasValue)
                nums.Add(v.Value);
        }

        if (nums.Count >= 4)
        {
            if (row.CoilSensibleMbh is null)
                row.CoilSensibleMbh = nums[^4];
            if (row.CoilTotalMbh is null)
                row.CoilTotalMbh = nums[^3];
            if (row.TotalCoolingAirflowCfm is null)
                row.TotalCoolingAirflowCfm = nums[^1];
        }
        else if (nums.Count == 3)
        {
            if (row.CoilSensibleMbh is null)
                row.CoilSensibleMbh = nums[0];
            if (row.CoilTotalMbh is null)
                row.CoilTotalMbh = nums[1];
            if (row.TotalCoolingAirflowCfm is null)
                row.TotalCoolingAirflowCfm = nums[2];
        }
    }

    private static void ParseEngineeringChecks(List<PdfLine> lines, TraceDesignCoolingRoomExtract row, double pageWidth)
    {
        var g = lines.FindIndex(l =>
            l.Text.Contains("General Engineering Checks", StringComparison.OrdinalIgnoreCase));
        if (g < 0)
            return;

        for (var i = g + 1; i < Math.Min(g + 45, lines.Count); i++)
        {
            var t = lines[i].Text;
            if (t.Contains("Total Cooling Load", StringComparison.OrdinalIgnoreCase) &&
                !t.Contains("Coil", StringComparison.OrdinalIgnoreCase))
            {
                var v = ExtractRightColumnNumber(lines[i], pageWidth);
                if (!v.HasValue && i + 1 < lines.Count)
                    v = ExtractRightColumnNumber(lines[i + 1], pageWidth);
                row.EngTotalCoolingLoad = v;
                break;
            }
        }

        for (var i = g + 1; i < Math.Min(g + 45, lines.Count); i++)
        {
            var nt = lines[i].NormalizedText;
            if (nt.Contains("Area", StringComparison.OrdinalIgnoreCase) &&
                nt.Contains("Load", StringComparison.OrdinalIgnoreCase) &&
                nt.Contains("/"))
            {
                var v = ExtractRightColumnNumber(lines[i], pageWidth);
                if (!v.HasValue && i + 1 < lines.Count)
                    v = ExtractRightColumnNumber(lines[i + 1], pageWidth);
                row.EngAreaPerLoad = v;
                break;
            }
        }
    }

    private static double? ExtractRightColumnNumber(PdfLine line, double pageWidth)
    {
        var threshold = pageWidth * 0.38;
        foreach (var w in line.Words.Where(x => x.Left > threshold).OrderByDescending(x => x.Left))
        {
            var v = TryParseNumber(w.Text);
            if (v.HasValue)
                return v;
        }

        foreach (var w in line.Words.OrderByDescending(x => x.Left))
        {
            var v = TryParseNumber(w.Text);
            if (v.HasValue)
                return v;
        }

        return null;
    }

    private static double? ParseCommaDouble(string text)
    {
        var cleaned = text.Replace(",", "", StringComparison.Ordinal).Trim();
        return double.TryParse(cleaned, NumberStyles.Float, Invariant, out var v) ? v : null;
    }

    private static List<PdfLine> GetPageLines(Page page)
    {
        var words = page.GetWords()
            .Select(w => new PdfWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Bottom,
                w.BoundingBox.Top, w.BoundingBox.Right))
            .ToList();

        return GroupWordsIntoLines(words);
    }

    private static List<PdfLine> GroupWordsIntoLines(List<PdfWord> words)
    {
        if (words.Count == 0) return new List<PdfLine>();

        var sorted = words.OrderByDescending(w => w.Bottom).ThenBy(w => w.Left).ToList();
        var lines = new List<PdfLine>();
        var currentLine = new PdfLine { Y = sorted[0].Bottom };
        currentLine.Words.Add(sorted[0]);

        for (var i = 1; i < sorted.Count; i++)
        {
            if (Math.Abs(sorted[i].Bottom - currentLine.Y) <= LineGroupTolerance)
            {
                currentLine.Words.Add(sorted[i]);
            }
            else
            {
                currentLine.Words.Sort((a, b) => a.Left.CompareTo(b.Left));
                lines.Add(currentLine);
                currentLine = new PdfLine { Y = sorted[i].Bottom };
                currentLine.Words.Add(sorted[i]);
            }
        }

        currentLine.Words.Sort((a, b) => a.Left.CompareTo(b.Left));
        lines.Add(currentLine);
        return lines;
    }

    private static double? TryParseNumber(string text)
    {
        var cleaned = text.Trim().TrimEnd('.', ',').Replace(",", "", StringComparison.Ordinal);
        if (Regex.IsMatch(cleaned, @"^-?\d+(\.\d+)?$") &&
            double.TryParse(cleaned, NumberStyles.Float, Invariant, out var value))
            return value;

        return null;
    }
}
