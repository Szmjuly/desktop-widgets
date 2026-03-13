using UglyToad.PdfPig;

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
                var text = string.Join(" ", page.GetWords().Select(w => w.Text));

                if (text.Contains("Zone Sizing Summary for", StringComparison.OrdinalIgnoreCase))
                    return PdfType.ZoneSizingSummary;

                if (text.Contains("Design Load Summary for", StringComparison.OrdinalIgnoreCase))
                    return PdfType.SpaceDesignLoadSummary;

                if (text.Contains("Air System Sizing Summary for", StringComparison.OrdinalIgnoreCase))
                    return PdfType.AirSystemSizingSummary;
            }
        }
        catch
        {
            // If we can't open or read the PDF, it's unknown
        }

        return PdfType.Unknown;
    }
}
