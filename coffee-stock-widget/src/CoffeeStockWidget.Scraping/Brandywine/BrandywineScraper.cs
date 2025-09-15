using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using CoffeeStockWidget.Core.Abstractions;
using CoffeeStockWidget.Core.Models;
using CoffeeStockWidget.Core.Services;

namespace CoffeeStockWidget.Scraping.Brandywine;

public class BrandywineScraper : ISiteScraper
{
    private static readonly Uri BaseUri = new("https://www.brandywinecoffeeroasters.com/");

    private readonly IHttpFetcher _http;
    private readonly IBrowsingContext _ctx;

    public BrandywineScraper(IHttpFetcher httpFetcher)
    {
        _http = httpFetcher;
        var config = Configuration.Default;
        _ctx = BrowsingContext.New(config);
    }

    public async Task<IReadOnlyList<CoffeeItem>> FetchAsync(Source source, CancellationToken ct = default)
    {
        var collectionUri = source.RootUrl ?? new Uri(BaseUri, "/collections/all-coffee-1");
        var html = await _http.GetStringAsync(collectionUri, null, ct).ConfigureAwait(false);
        var doc = await _ctx.OpenAsync(req => req.Content(html), ct).ConfigureAwait(false);

        var items = ExtractItems(doc, source);
        return items;
    }

    private static List<CoffeeItem> ExtractItems(IDocument doc, Source source)
    {
        // Shopify collection pages: product links under collections often include "/collections/.../products/"
        var anchors = doc.QuerySelectorAll("a")
            .Where(a =>
                a.GetAttribute("href")?.Contains("/collections/", StringComparison.OrdinalIgnoreCase) == true &&
                a.GetAttribute("href")?.Contains("/products/", StringComparison.OrdinalIgnoreCase) == true)
            .OfType<IHtmlAnchorElement>()
            .ToList();

        var byUrl = new Dictionary<string, (Uri Url, string AggregateText, string ContainerText)>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in anchors)
        {
            var href = a.GetAttribute("href") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(href)) continue;
            var absolute = MakeAbsolute(href);

            var text = a.Text().Trim();
            var container = a.Closest("li,div,article,product,product-card") ?? a.ParentElement;
            var containerText = (container?.TextContent ?? string.Empty).Trim();

            if (byUrl.TryGetValue(absolute.ToString(), out var existing))
            {
                var agg = existing.AggregateText;
                if (!string.IsNullOrWhiteSpace(text)) agg += "\n" + text;
                var cont = string.IsNullOrWhiteSpace(existing.ContainerText) ? containerText : existing.ContainerText;
                byUrl[absolute.ToString()] = (absolute, agg, cont);
            }
            else
            {
                byUrl[absolute.ToString()] = (absolute, text, containerText);
            }
        }

        var results = new List<CoffeeItem>();
        foreach (var kv in byUrl.Values)
        {
            var aggregated = (kv.AggregateText + "\n" + kv.ContainerText).Trim();
            var title = ExtractTitle(aggregated);
            var priceCents = ExtractPriceCents(aggregated);
            var inStock = kv.ContainerText.IndexOf("sold out", StringComparison.OrdinalIgnoreCase) < 0
                          && kv.ContainerText.IndexOf("out of stock", StringComparison.OrdinalIgnoreCase) < 0;

            if (string.IsNullOrWhiteSpace(title))
            {
                var seg = kv.Url.Segments.LastOrDefault()?.Trim('/') ?? string.Empty;
                title = Uri.UnescapeDataString(seg.Replace('-', ' '));
            }

            var item = new CoffeeItem
            {
                SourceId = source.Id ?? 0,
                Title = title,
                Url = kv.Url,
                PriceCents = priceCents,
                InStock = inStock,
                ItemKey = Normalization.ComputeStableKey(title, kv.Url),
                FirstSeenUtc = DateTimeOffset.UtcNow,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            results.Add(item);
        }

        return results;
    }

    private static Uri MakeAbsolute(string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var abs)) return abs;
        return new Uri(BaseUri, href);
    }

    private static string ExtractTitle(string anchorText)
    {
        var lines = anchorText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (lines.Count == 0) return anchorText.Trim();
        return lines[0];
    }

    private static int? ExtractPriceCents(string text)
    {
        var m = Regex.Match(text, @"\$\s*([0-9]+(?:\.[0-9]{2})?)");
        if (!m.Success) return null;
        if (decimal.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            return (int)Math.Round(d * 100m);
        }
        return null;
    }
}
