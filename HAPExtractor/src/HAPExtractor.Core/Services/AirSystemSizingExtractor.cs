using System.Text.RegularExpressions;
using HAPExtractor.Core.Models;
using UglyToad.PdfPig;

namespace HAPExtractor.Core.Services;

public class AirSystemSizingExtractor
{
    /// <summary>
    /// Extract air system sizing data from an Air System Sizing Summary PDF.
    /// Returns one entry per air system with SystemName and ft²/Ton.
    /// </summary>
    public List<AirSystemSizingData> Extract(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        return Extract(document, Enumerable.Range(1, document.NumberOfPages).ToList());
    }

    public List<AirSystemSizingData> Extract(PdfDocument document, IReadOnlyList<int> pages)
    {
        var results = new List<AirSystemSizingData>();

        var pageList = pages
            .Where(p => p >= 1 && p <= document.NumberOfPages)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        string? currentSystemName = null;
        var currentPageTexts = new List<string>();

        foreach (var pageIndex in pageList)
        {
            var page = document.GetPage(pageIndex);
            var pageText = string.Join(" ", page.GetWords().Select(w => w.Text));

            var headerMatch = Regex.Match(pageText,
                @"Air\s+System\s+Sizing\s+Summary\s+for\s+(\S+)",
                RegexOptions.IgnoreCase);

            if (headerMatch.Success)
            {
                if (currentSystemName != null && currentPageTexts.Count > 0)
                {
                    var data = ParseSystemPages(currentSystemName, currentPageTexts);
                    if (data != null) results.Add(data);
                }

                currentSystemName = headerMatch.Groups[1].Value.Trim();
                currentPageTexts = new List<string> { pageText };
            }
            else if (currentSystemName != null)
            {
                currentPageTexts.Add(pageText);
            }
        }

        if (currentSystemName != null && currentPageTexts.Count > 0)
        {
            var data = ParseSystemPages(currentSystemName, currentPageTexts);
            if (data != null) results.Add(data);
        }

        return results;
    }

    private AirSystemSizingData? ParseSystemPages(string systemName, List<string> pageTexts)
    {
        var combined = string.Join(" ", pageTexts);

        var data = new AirSystemSizingData { SystemName = systemName };

        // Extract ft²/Ton (may appear as "ft²/Ton" or "ft2/Ton" or "ft /Ton")
        var sqftPerTonMatch = Regex.Match(combined,
            @"ft[²2\s]*/Ton[\s.]*?([\d,]+\.?\d*)",
            RegexOptions.IgnoreCase);
        if (sqftPerTonMatch.Success)
        {
            double.TryParse(sqftPerTonMatch.Groups[1].Value.Replace(",", ""), out double val);
            data.SqftPerTon = val;
        }

        // Extract Floor Area
        var floorAreaMatch = Regex.Match(combined,
            @"Floor\s+Area[\s.]*?([\d,]+\.?\d*)",
            RegexOptions.IgnoreCase);
        if (floorAreaMatch.Success)
        {
            double.TryParse(floorAreaMatch.Groups[1].Value.Replace(",", ""), out double val);
            data.FloorArea = val;
        }

        // Extract Total coil load (Tons)
        var tonsMatch = Regex.Match(combined,
            @"Total\s+coil\s+load[\s.]*?([\d,]+\.?\d*)\s*Tons",
            RegexOptions.IgnoreCase);
        if (tonsMatch.Success)
        {
            double.TryParse(tonsMatch.Groups[1].Value.Replace(",", ""), out double val);
            data.TotalCoilLoadTons = val;
        }

        // Extract CFM/Ton
        var cfmPerTonMatch = Regex.Match(combined,
            @"CFM/Ton[\s.]*?([\d,]+\.?\d*)",
            RegexOptions.IgnoreCase);
        if (cfmPerTonMatch.Success)
        {
            double.TryParse(cfmPerTonMatch.Groups[1].Value.Replace(",", ""), out double val);
            data.CfmPerTon = val;
        }

        return data;
    }
}
