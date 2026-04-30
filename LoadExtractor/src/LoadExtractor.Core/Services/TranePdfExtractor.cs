using System.Globalization;
using System.Text.RegularExpressions;
using LoadExtractor.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LoadExtractor.Core.Services;

public class TranePdfExtractor
{
    private const double LineGroupTolerance = 3.0;

    private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

    private record PdfWord(string Text, double Left, double Bottom, double Top, double Right)
    {
        public double CenterX => (Left + Right) / 2.0;
    }

    private class PdfLine
    {
        public double Y { get; init; }
        public List<PdfWord> Words { get; } = new();
        public string Text => string.Join(" ", Words.OrderBy(w => w.Left).Select(w => w.Text));
        public string NormalizedText => Text.Replace("\u00b2", "2", StringComparison.Ordinal);
    }

    private record ColumnAnchors(
        double TotalTons,
        double TotalMbh,
        double SensibleMbh,
        double CoilAirflow,
        double GrossFloorArea);

    public List<TraneRoomLoad> Extract(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        return Extract(document, Enumerable.Range(1, document.NumberOfPages).ToList());
    }

    public List<TraneRoomLoad> Extract(PdfDocument document, IReadOnlyList<int> pages)
    {
        var results = new List<TraneRoomLoad>();

        var pageList = pages
            .Where(p => p >= 1 && p <= document.NumberOfPages)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        foreach (var pageIndex in pageList)
        {
            var page = document.GetPage(pageIndex);
            var lines = GetPageLines(page);
            var room = ParseRoomPage(lines, pageIndex, page.Height);
            if (room != null)
                results.Add(room);
        }

        return results;
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
                SortLineWords(currentLine);
                lines.Add(currentLine);
                currentLine = new PdfLine { Y = sorted[i].Bottom };
                currentLine.Words.Add(sorted[i]);
            }
        }

        SortLineWords(currentLine);
        lines.Add(currentLine);
        return lines;
    }

    private static void SortLineWords(PdfLine line)
    {
        line.Words.Sort((left, right) => left.Left.CompareTo(right.Left));
    }

    private static TraneRoomLoad? ParseRoomPage(List<PdfLine> lines, int pageIndex, double pageHeight)
    {
        if (!LooksLikeTraneRoomPage(lines))
            return null;

        var roomLine = FindRoomLine(lines, pageHeight);
        if (roomLine == null)
            return null;

        var roomNumber = roomLine.Words[0].Text.Trim();
        var roomName = string.Join(" ", roomLine.Words.Skip(1).Select(w => w.Text)).Trim();
        var projectName = ExtractProjectName(lines);
        var anchors = FindColumnAnchors(lines);
        var mainCoolingLine = FindMainCoolingLine(lines);

        var room = new TraneRoomLoad
        {
            SourcePage = pageIndex,
            ProjectName = projectName,
            RoomNumber = roomNumber,
            RoomName = roomName
        };

        if (mainCoolingLine != null)
        {
            room.TotalCapacityTons = FindNumberNearX(mainCoolingLine, anchors.TotalTons, 16);
            room.TotalCapacityMbh = FindNumberNearX(mainCoolingLine, anchors.TotalMbh, 18);
            room.SensibleCapacityMbh = FindNumberNearX(mainCoolingLine, anchors.SensibleMbh, 18);
            room.CoilAirflowCfm = FindNumberNearX(mainCoolingLine, anchors.CoilAirflow, 22);
            room.GrossFloorAreaSqFt = FindNumberNearX(mainCoolingLine, anchors.GrossFloorArea, 28);
        }

        room.SqFtPerTon = ExtractEngineeringValue(lines, IsSqFtPerTonLine);
        room.NumberOfPeople = ExtractEngineeringValue(lines, IsPeopleLine);

        return room;
    }

    private static bool LooksLikeTraneRoomPage(List<PdfLine> lines)
    {
        return lines.Any(l => l.Text.Contains("Room Checksums", StringComparison.OrdinalIgnoreCase)) &&
               lines.Any(l => l.Text.Contains("Cooling Coil Selection", StringComparison.OrdinalIgnoreCase));
    }

    private static PdfLine? FindRoomLine(List<PdfLine> lines, double pageHeight)
    {
        var minimumY = pageHeight * 0.70;

        return lines
            .Where(l => l.Y >= minimumY &&
                        l.Words.Count >= 2 &&
                        l.Words[0].Left < 90 &&
                        Regex.IsMatch(l.Words[0].Text, @"^\d+[A-Za-z]?$"))
            .OrderByDescending(l => l.Y)
            .FirstOrDefault();
    }

    private static string ExtractProjectName(List<PdfLine> lines)
    {
        foreach (var line in lines)
        {
            if (!line.Text.Contains("Project", StringComparison.OrdinalIgnoreCase) ||
                !line.Text.Contains("Name", StringComparison.OrdinalIgnoreCase))
                continue;

            var words = line.Words.OrderBy(w => w.Left).ToList();
            var markerIndex = words.FindIndex(w => w.Text.StartsWith("Name", StringComparison.OrdinalIgnoreCase));
            if (markerIndex < 0)
                continue;

            var nameWords = words
                .Skip(markerIndex + 1)
                .TakeWhile(w => w.Left < 520 && !IsProjectFooterStopWord(w.Text))
                .Select(w => w.Text.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            if (nameWords.Count > 0)
                return string.Join(" ", nameWords).Trim();
        }

        return "Unknown Project";
    }

    private static bool IsProjectFooterStopWord(string text)
    {
        return text.Equals("TRACE", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("Calculated", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("Page", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(text, @"^\d{1,2}/\d{1,2}/\d{2,4}$");
    }

    private static ColumnAnchors FindColumnAnchors(List<PdfLine> lines)
    {
        var anchors = new ColumnAnchors(
            TotalTons: 116,
            TotalMbh: 151,
            SensibleMbh: 198,
            CoilAirflow: 255,
            GrossFloorArea: 494);

        var heading = lines.FirstOrDefault(l =>
            l.Text.Contains("Cooling Coil Selection", StringComparison.OrdinalIgnoreCase));

        if (heading != null)
        {
            var unitLine = lines
                .Where(l => l.Y < heading.Y && l.Y > heading.Y - 32)
                .OrderByDescending(l => l.Y)
                .FirstOrDefault(l =>
                    l.Words.Any(w => w.Text.Equals("ton", StringComparison.OrdinalIgnoreCase)) &&
                    l.Words.Count(w => w.Text.Equals("MBh", StringComparison.OrdinalIgnoreCase)) >= 2 &&
                    l.Words.Any(w => w.Text.Equals("cfm", StringComparison.OrdinalIgnoreCase)));

            if (unitLine != null)
            {
                var ton = unitLine.Words.FirstOrDefault(w => w.Text.Equals("ton", StringComparison.OrdinalIgnoreCase));
                var mbhWords = unitLine.Words
                    .Where(w => w.Text.Equals("MBh", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(w => w.Left)
                    .ToList();
                var cfm = unitLine.Words.FirstOrDefault(w => w.Text.Equals("cfm", StringComparison.OrdinalIgnoreCase));

                if (ton != null && mbhWords.Count >= 2 && cfm != null)
                {
                    anchors = anchors with
                    {
                        TotalTons = ton.CenterX,
                        TotalMbh = mbhWords[0].CenterX,
                        SensibleMbh = mbhWords[1].CenterX,
                        CoilAirflow = cfm.CenterX
                    };
                }
            }
        }

        var grossHeaderLine = lines.FirstOrDefault(l =>
            l.Text.Contains("Gross", StringComparison.OrdinalIgnoreCase) &&
            l.Text.Contains("Total", StringComparison.OrdinalIgnoreCase));

        if (grossHeaderLine != null)
        {
            var grossWord = grossHeaderLine.Words.FirstOrDefault(w =>
                w.Text.Equals("Gross", StringComparison.OrdinalIgnoreCase));
            var totalWord = grossHeaderLine.Words.FirstOrDefault(w =>
                w.Text.Equals("Total", StringComparison.OrdinalIgnoreCase) && w.Left > (grossWord?.Left ?? 0));

            if (grossWord != null && totalWord != null)
            {
                anchors = anchors with
                {
                    GrossFloorArea = (grossWord.CenterX + totalWord.CenterX) / 2.0 + 20
                };
            }
        }

        return anchors;
    }

    private static PdfLine? FindMainCoolingLine(List<PdfLine> lines)
    {
        var heading = lines.FirstOrDefault(l =>
            l.Text.Contains("Cooling Coil Selection", StringComparison.OrdinalIgnoreCase));

        var candidates = lines.Where(IsMainCoolingLine).ToList();
        if (heading == null)
            return candidates.FirstOrDefault();

        return candidates
            .Where(l => l.Y < heading.Y && l.Y > heading.Y - 70)
            .OrderByDescending(l => l.Y)
            .FirstOrDefault() ?? candidates.FirstOrDefault();
    }

    private static bool IsMainCoolingLine(PdfLine line)
    {
        var words = line.Words.Select(w => w.Text).ToList();
        return words.Any(w => w.Equals("Main", StringComparison.OrdinalIgnoreCase)) &&
               words.Any(w => w.Equals("Clg", StringComparison.OrdinalIgnoreCase));
    }

    private static double? ExtractEngineeringValue(List<PdfLine> lines, Func<PdfLine, bool> predicate)
    {
        var line = lines.FirstOrDefault(predicate);
        if (line == null)
            return null;

        return lines
            .SelectMany(l => l.Words)
            .Where(w => Math.Abs(w.Bottom - line.Y) <= 4.5)
            .Where(w => w.Left > 630)
            .Select(w => new { Word = w, Value = TryParseNumber(w.Text) })
            .Where(x => x.Value.HasValue)
            .OrderBy(x => x.Word.Left)
            .Select(x => x.Value)
            .FirstOrDefault();
    }

    private static bool IsSqFtPerTonLine(PdfLine line)
    {
        var text = line.NormalizedText;
        return Regex.IsMatch(text, @"ft\s*2\s*/\s*ton", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(text, @"ft\s*/\s*ton", RegexOptions.IgnoreCase);
    }

    private static bool IsPeopleLine(PdfLine line)
    {
        var text = line.Text;
        return text.Contains("People", StringComparison.OrdinalIgnoreCase) &&
               (line.Words.Any(w => w.Text.Equals("No.", StringComparison.OrdinalIgnoreCase) ||
                                    w.Text.Equals("No", StringComparison.OrdinalIgnoreCase)) ||
                text.Contains("Number", StringComparison.OrdinalIgnoreCase));
    }

    private static double? FindNumberNearX(PdfLine line, double targetX, double tolerance)
    {
        return line.Words
            .Select(w => new { Word = w, Value = TryParseNumber(w.Text) })
            .Where(x => x.Value.HasValue && Math.Abs(x.Word.CenterX - targetX) <= tolerance)
            .OrderBy(x => Math.Abs(x.Word.CenterX - targetX))
            .Select(x => x.Value)
            .FirstOrDefault();
    }

    private static double? TryParseNumber(string text)
    {
        var cleaned = text.Trim().TrimEnd('.', ',').Replace(",", "", StringComparison.Ordinal);
        if (Regex.IsMatch(cleaned, @"^-?\d+(\.\d+)?$") &&
            double.TryParse(cleaned, NumberStyles.Float, InvariantCulture, out var value))
        {
            return value;
        }

        return null;
    }
}
