using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;

namespace CoffeeStockWidget.DevHarness;

internal static class DomNotesParser
{
    private static string Clean(string s)
    {
        try
        {
            s = System.Net.WebUtility.HtmlDecode(s);
            s = s.Replace("\r", " ").Replace("\n", " ");
            s = System.Text.RegularExpressions.Regex.Replace(s, "\\s+", " ").Trim();
            if (s.Length > 300) s = s.Substring(0, 300);
            return s;
        }
        catch { return s; }
    }

    public static async Task<string?> TryExtractAsync(string html, string parserType)
    {
        try
        {
            var ctx = BrowsingContext.New(Configuration.Default);
            var doc = await ctx.OpenAsync(req => req.Content(html)).ConfigureAwait(false);
            if (string.Equals(parserType, "BlackAndWhite", StringComparison.OrdinalIgnoreCase))
            {
                var bw = TryExtractBw(doc);
                if (!string.IsNullOrWhiteSpace(bw)) return bw;
            }
            else if (string.Equals(parserType, "Brandywine", StringComparison.OrdinalIgnoreCase))
            {
                var br = TryExtractBrandywine(doc);
                if (!string.IsNullOrWhiteSpace(br)) return br;
            }
        }
        catch { }
        return null;
    }

    private static string? TryExtractBw(IDocument doc)
    {
        // Broaden search: any element containing heading text "How it tastes" or "Take a sip"
        var markers = doc.All
            .Where(e =>
                !string.IsNullOrWhiteSpace(e.TextContent) && (
                    e.TextContent!.IndexOf("How it tastes", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    e.TextContent!.IndexOf("Take a sip", StringComparison.OrdinalIgnoreCase) >= 0))
            .Take(6)
            .ToList();

        foreach (var m in markers)
        {
            // Prefer nearest semantic container
            var container = m.Closest("details, .disclosure, .product__accordion, .accordion, .collapsible-content, .accordion__item, .product__accordion-item")
                           ?? m.ParentElement
                           ?? m as IElement;

            if (container == null) continue;

            // Special case: within the description block, a paragraph starting with "TAKE A SIP |"
            var pSip = container.QuerySelectorAll("p").FirstOrDefault(p => (p.TextContent ?? string.Empty).IndexOf("Take a sip", StringComparison.OrdinalIgnoreCase) >= 0);
            if (pSip != null)
            {
                var s = pSip.TextContent ?? string.Empty;
                var l = s.ToLowerInvariant();
                var idx = l.IndexOf("take a sip");
                if (idx >= 0)
                {
                    // Prefer text after a '|' or ':' if present, else after the phrase
                    var pos = s.IndexOf('|', idx);
                    if (pos < 0) pos = s.IndexOf(':', idx);
                    if (pos >= 0 && pos + 1 < s.Length) pos = pos + 1; else pos = idx + "take a sip".Length;
                    if (pos < s.Length)
                    {
                        var tail = s.Substring(pos).Trim();
                        if (!string.IsNullOrWhiteSpace(tail)) return Clean(tail);
                    }
                }
            }

            // Probe known content containers in order of likelihood
            var contentNodes = container.QuerySelectorAll(
                ".disclosure__content, .product__accordion-content, .accordion__content, .collapsible-content__inner, .disclosure__panel, [role='region]', .accordion__panel, .product__accordion__content");

            var aggregated = string.Join(" ", contentNodes.Select(n => (n.TextContent ?? string.Empty).Trim()).Where(t => t.Length > 0));
            aggregated = Clean(aggregated);
            if (!string.IsNullOrWhiteSpace(aggregated))
            {
                // Avoid sections that are clearly not notes
                var l = aggregated.ToLowerInvariant();
                if (!(l.Contains("how we got there") || l.Contains("coffee info") || l.Contains("process:") || l.Contains("variety:") || l.Contains("origin:")))
                    return aggregated;
            }

            // Fallback: scan siblings after the heading for first substantial paragraph/div
            var start = m as IElement ?? m.ParentElement;
            if (start != null)
            {
                var sib = start.NextElementSibling;
                var sb = new StringBuilder();
                while (sib != null)
                {
                    var txt = sib.TextContent?.Trim();
                    if (!string.IsNullOrWhiteSpace(txt))
                    {
                        var c = Clean(txt);
                        var low = c.ToLowerInvariant();
                        if (low.Contains("how we got there") || low.StartsWith("coffee info") || low.StartsWith("about")) break;
                        if (sb.Length > 0) sb.Append(' ');
                        sb.Append(c);
                        if (sb.Length > 300) break;
                    }
                    // stop scanning when we hit another accordion/heading-like element
                    if ((sib.ClassName ?? string.Empty).IndexOf("accordion", StringComparison.OrdinalIgnoreCase) >= 0 || sib.NodeName.StartsWith("H")) break;
                    sib = sib.NextElementSibling;
                }
                var res = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(res)) return res;
            }
        }
        return null;
    }

    private static string? TryExtractBrandywine(IDocument doc)
    {
        // Find text nodes under .rte or product description that contain "We Taste:"
        var containers = doc.QuerySelectorAll(".rte, .product__description, .product, main, article");
        foreach (var c in containers)
        {
            // First, try <p> elements
            foreach (var p in c.QuerySelectorAll("p"))
            {
                var txt = p.TextContent ?? string.Empty;
                if (txt.IndexOf("We Taste", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var s = txt;
                    var idx = s.IndexOf(":");
                    if (idx >= 0 && idx + 1 < s.Length)
                    {
                        var tail = s.Substring(idx + 1).Trim();
                        if (!string.IsNullOrWhiteSpace(tail)) return Clean(tail);
                    }
                    // Sometimes "We Taste:" is in a <strong>, and values are sibling text/spans
                    var after = p.ChildNodes.SkipWhile(n => (n.TextContent ?? "").IndexOf("We Taste", StringComparison.OrdinalIgnoreCase) < 0)
                                             .Skip(1)
                                             .Select(n => n.TextContent?.Trim())
                                             .Where(t => !string.IsNullOrWhiteSpace(t))
                                             .ToList();
                    if (after.Count > 0) return Clean(string.Join(" ", after));
                }
            }
            // Otherwise, scan all text content for the phrase
            var text = c.TextContent ?? string.Empty;
            var idx2 = text.IndexOf("We Taste", StringComparison.OrdinalIgnoreCase);
            if (idx2 >= 0)
            {
                var slice = text.Substring(idx2);
                // Pull up to first newline or period
                var endIdx = slice.IndexOf('\n');
                if (endIdx < 0) endIdx = slice.IndexOf('.');
                if (endIdx > 0) slice = slice.Substring(0, endIdx);
                var colon = slice.IndexOf(":");
                if (colon >= 0 && colon + 1 < slice.Length)
                {
                    var tail = slice.Substring(colon + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(tail)) return Clean(tail);
                }
            }
        }
        return null;
    }
}
