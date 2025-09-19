using System;
using System.Linq;
using System.Text;
using AngleSharp;
using AngleSharp.Dom;

namespace CoffeeStockWidget.Scraping;

public static class NotesDomExtractor
{
    public static string? TryExtract(string html, string parserType)
    {
        try
        {
            var ctx = BrowsingContext.New(Configuration.Default);
            var doc = ctx.OpenAsync(req => req.Content(html)).GetAwaiter().GetResult();
            if (string.Equals(parserType, "BlackAndWhite", StringComparison.OrdinalIgnoreCase))
            {
                var bw = TryExtractBw(doc);
                if (!string.IsNullOrWhiteSpace(bw)) return Clean(bw!);
            }
            else if (string.Equals(parserType, "Brandywine", StringComparison.OrdinalIgnoreCase))
            {
                var br = TryExtractBrandywine(doc);
                if (!string.IsNullOrWhiteSpace(br)) return Clean(br!);
            }
        }
        catch { }
        return null;
    }

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

    private static string? TryExtractBw(IDocument doc)
    {
        var markers = doc.All
            .Where(e => !string.IsNullOrWhiteSpace(e.TextContent) && (
                e.TextContent!.IndexOf("How it tastes", StringComparison.OrdinalIgnoreCase) >= 0 ||
                e.TextContent!.IndexOf("Take a sip", StringComparison.OrdinalIgnoreCase) >= 0))
            .Take(6)
            .ToList();

        foreach (var m in markers)
        {
            var container = m.Closest("details, .disclosure, .product__accordion, .accordion, .collapsible-content, .accordion__item, .product__accordion-item")
                           ?? m.ParentElement
                           ?? m as IElement;
            if (container == null) continue;

            // Prefer the details that owns this heading
            var details = m.Closest("details") ?? container;

            // Probe known content containers within this details
            var contentNodes = details.QuerySelectorAll(
                ".disclosure__content, .product__accordion-content, .accordion__content, .collapsible-content__inner, .disclosure__panel, [role='region]', .accordion__panel, .product__accordion__content");
            // Special-case: a paragraph starting with "TAKE A SIP" often contains flavor text
            var pSip = details.QuerySelectorAll("p").FirstOrDefault(p => (p.TextContent ?? string.Empty).IndexOf("Take a sip", StringComparison.OrdinalIgnoreCase) >= 0);
            if (pSip != null)
            {
                var s = pSip.TextContent ?? string.Empty;
                var l = s.ToLowerInvariant();
                var idx = l.IndexOf("take a sip");
                if (idx >= 0)
                {
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

            // Alternate structure: a <strong> heading inside the same paragraph/div
            var strongSip = details.QuerySelectorAll("strong").FirstOrDefault(st => (st.TextContent ?? string.Empty).IndexOf("Take a sip", StringComparison.OrdinalIgnoreCase) >= 0);
            if (strongSip != null)
            {
                // Prefer text from the parent block (p/div) starting after the strong
                var parent = strongSip.Closest("p, div") ?? strongSip.ParentElement;
                if (parent != null)
                {
                    var textAfter = new StringBuilder();
                    bool past = false;
                    foreach (var node in parent.ChildNodes)
                    {
                        if (!past)
                        {
                            if (node == strongSip || (node.TextContent ?? string.Empty).IndexOf("Take a sip", StringComparison.OrdinalIgnoreCase) >= 0) { past = true; }
                            continue;
                        }
                        var t = node.TextContent?.Trim();
                        if (!string.IsNullOrWhiteSpace(t))
                        {
                            // Stop when the next section heading starts
                            var tl = t.ToLowerInvariant();
                            if (tl.StartsWith("origin |") || tl.StartsWith("producer |") || tl.StartsWith("farm |") || tl.StartsWith("process |") || tl.StartsWith("variety |") || tl.StartsWith("elevation |") || tl.StartsWith("meet the producer") || tl.StartsWith("trust the process"))
                                break;
                            if (textAfter.Length > 0) textAfter.Append(' ');
                            textAfter.Append(t);
                            if (textAfter.Length > 300) break;
                        }
                    }
                    var res = Clean(textAfter.ToString());
                    if (!string.IsNullOrWhiteSpace(res)) return res;
                }
            }

            var aggregated = string.Join(" ", contentNodes.Select(n => (n.TextContent ?? string.Empty).Trim()).Where(t => t.Length > 0));
            aggregated = Clean(aggregated);
            if (!string.IsNullOrWhiteSpace(aggregated))
            {
                var l = aggregated.ToLowerInvariant();
                // If this block includes other sections, trim to the portion after heading and before next obvious section
                var idxHead = l.IndexOf("how it tastes");
                if (idxHead >= 0) aggregated = aggregated.Substring(idxHead + "how it tastes".Length).Trim();
                var cut = FirstIndexOfAny(l, new[] { "how we got there", "coffee info", "process:", "variety:", "origin:" });
                if (cut >= 0 && cut < aggregated.Length && cut > 0)
                {
                    aggregated = aggregated.Substring(0, cut).Trim();
                }
                if (!string.IsNullOrWhiteSpace(aggregated)) return aggregated;
            }

            // Fallback: siblings after the marker
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
                    if ((sib.ClassName ?? string.Empty).IndexOf("accordion", StringComparison.OrdinalIgnoreCase) >= 0 || sib.NodeName.StartsWith("H")) break;
                    sib = sib.NextElementSibling;
                }
                var res = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(res)) return res;
            }
        }
        return null;
    }

    private static int FirstIndexOfAny(string sLower, string[] needlesLower)
    {
        int best = -1;
        foreach (var n in needlesLower)
        {
            var i = sLower.IndexOf(n, StringComparison.OrdinalIgnoreCase);
            if (i >= 0 && (best < 0 || i < best)) best = i;
        }
        return best;
    }

    private static string? TryExtractBrandywine(IDocument doc)
    {
        var containers = doc.QuerySelectorAll(".rte, .product__description, .product, main, article");
        foreach (var c in containers)
        {
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
                        if (!string.IsNullOrWhiteSpace(tail)) return tail;
                    }
                    var after = p.ChildNodes
                        .SkipWhile(n => (n.TextContent ?? "").IndexOf("We Taste", StringComparison.OrdinalIgnoreCase) < 0)
                        .Skip(1)
                        .Select(n => n.TextContent?.Trim())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();
                    if (after.Count > 0) return string.Join(" ", after);
                }
            }
            var text = c.TextContent ?? string.Empty;
            var idx2 = text.IndexOf("We Taste", StringComparison.OrdinalIgnoreCase);
            if (idx2 >= 0)
            {
                var slice = text.Substring(idx2);
                var endIdx = slice.IndexOf('\n');
                if (endIdx < 0) endIdx = slice.IndexOf('.');
                if (endIdx > 0) slice = slice.Substring(0, endIdx);
                var colon = slice.IndexOf(":");
                if (colon >= 0 && colon + 1 < slice.Length)
                {
                    var tail = slice.Substring(colon + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(tail)) return tail;
                }
            }
        }
        return null;
    }
}
