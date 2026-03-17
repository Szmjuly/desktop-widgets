using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace HAPExtractor.Core.Services;

public enum PdfType
{
    ZoneSizingSummary,
    SpaceDesignLoadSummary,
    AirSystemSizingSummary,
    Unknown
}

public static class PdfClassifier
{
    /// <summary>
    /// Peek at the first few pages of a PDF to determine its type
    /// based on signature header text.
    /// </summary>
    public static PdfType Classify(string pdfPath)
    {
        try
        {
            using var document = PdfDocument.Open(pdfPath);
            int pagesToCheck = Math.Min(document.NumberOfPages, 3);

            for (int i = 1; i <= pagesToCheck; i++)
            {
                var page = document.GetPage(i);
                var pageType = ClassifyPage(page);
                if (pageType != PdfType.Unknown)
                    return pageType;
            }
        }
        catch
        {
            // If we can't open or read the PDF, it's unknown
        }

        return PdfType.Unknown;
    }

    public static PdfType ClassifyPage(Page page)
    {
        var text = string.Join(" ", page.GetWords().Select(w => w.Text));

        if (text.Contains("Zone Sizing Summary for", StringComparison.OrdinalIgnoreCase))
            return PdfType.ZoneSizingSummary;

        if (text.Contains("Design Load Summary for", StringComparison.OrdinalIgnoreCase))
            return PdfType.SpaceDesignLoadSummary;

        if (text.Contains("Air System Sizing Summary for", StringComparison.OrdinalIgnoreCase))
            return PdfType.AirSystemSizingSummary;

        return PdfType.Unknown;
    }

    /// <summary>
    /// Classify pages by PDF type based on header text.
    /// If a page has no recognizable header, it is assigned to the most recent known section
    /// (useful for continuation pages). Otherwise, it remains Unknown.
    /// </summary>
    public static Dictionary<PdfType, List<int>> ClassifyPages(PdfDocument document, bool assignUnknownToPrevious = true)
    {
        var buckets = new Dictionary<PdfType, List<int>>
        {
            [PdfType.ZoneSizingSummary] = new(),
            [PdfType.SpaceDesignLoadSummary] = new(),
            [PdfType.AirSystemSizingSummary] = new(),
            [PdfType.Unknown] = new(),
        };

        PdfType lastKnown = PdfType.Unknown;

        for (int pageIndex = 1; pageIndex <= document.NumberOfPages; pageIndex++)
        {
            var page = document.GetPage(pageIndex);
            var pageType = ClassifyPage(page);

            if (pageType == PdfType.Unknown && assignUnknownToPrevious && lastKnown != PdfType.Unknown)
            {
                buckets[lastKnown].Add(pageIndex);
                continue;
            }

            buckets[pageType].Add(pageIndex);
            if (pageType != PdfType.Unknown)
                lastKnown = pageType;
        }

        return buckets;
    }
}
