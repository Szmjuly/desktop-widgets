using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using CoffeeStockWidget.Core.Models;
using CoffeeStockWidget.Core.Services;
using CoffeeStockWidget.Infrastructure.Net;
using CoffeeStockWidget.Core.Abstractions;
using CoffeeStockWidget.Scraping.BlackAndWhite;
using CoffeeStockWidget.Scraping.Brandywine;

namespace CoffeeStockWidget.DevHarness;

public static class Harness
{
    public static async Task<int> RunAsync(string[] args)
    {
        // Args: --roaster all|bw|brandywine, --max N, --timeout seconds
        string roaster = "all";
        int max = 10;
        int timeoutSec = 120;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--roaster", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                roaster = args[++i];
            }
            else if (string.Equals(a, "--max", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var m))
            {
                i++; max = Math.Max(1, Math.Min(100, m));
            }
            else if (string.Equals(a, "--timeout", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var t))
            {
                i++; timeoutSec = Math.Max(10, Math.Min(600, t));
            }
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "HH:mm:ss.fff ";
                options.SingleLine = true;
            });
        });
        var log = loggerFactory.CreateLogger("Harness");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));
        var http = new HttpFetcher(minDelayPerHostMs: 1200);

        var sources = new List<Source>();
        if (string.Equals(roaster, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(roaster, "bw", StringComparison.OrdinalIgnoreCase) || string.Equals(roaster, "blackandwhite", StringComparison.OrdinalIgnoreCase))
        {
            sources.Add(new Source
            {
                Id = 1,
                Name = "Black & White Roasters",
                RootUrl = new Uri("https://www.blackwhiteroasters.com/collections/all-coffee"),
                ParserType = "BlackAndWhite",
                PollIntervalSeconds = 300,
                Enabled = true
            });
        }
        if (string.Equals(roaster, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(roaster, "brandywine", StringComparison.OrdinalIgnoreCase) || string.Equals(roaster, "bwine", StringComparison.OrdinalIgnoreCase))
        {
            sources.Add(new Source
            {
                Id = 2,
                Name = "Brandywine Coffee Roasters",
                RootUrl = new Uri("https://www.brandywinecoffeeroasters.com/collections/all-coffee-1"),
                ParserType = "Brandywine",
                PollIntervalSeconds = 300,
                Enabled = true
            });
        }

        if (sources.Count == 0)
        {
            log.LogError("No sources selected. Use --roaster all|bw|brandywine");
            return 1;
        }

        var overallSw = Stopwatch.StartNew();
        int totalProcessed = 0;
        int totalInStock = 0;
        int totalWithNotes = 0;
        int totalErrors = 0;
        int totalRegexHits = 0;
        int totalDomHits = 0;
        int totalNoisyDiscarded = 0;

        foreach (var src in sources)
        {
            using (log.BeginScope("Roaster={Roaster}", src.Name))
            {
                var scraper = GetScraper(src, http);

                var sw = Stopwatch.StartNew();
                log.LogInformation("Fetching collection: {Url}", src.RootUrl);
                IReadOnlyList<CoffeeItem> items;
                try
                {
                    items = await scraper.FetchAsync(src, cts.Token);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to fetch collection");
                    totalErrors++;
                    continue;
                }
                sw.Stop();
                log.LogInformation("Collection fetched: {Count} items in {Elapsed} ms", items.Count, sw.ElapsedMilliseconds);
                if (items.Count == 0) continue;

                int cap = Math.Max(1, Math.Min(max, items.Count));
                var toProcess = items.OrderBy(i => i.Title).Take(cap).ToList();
                int inStock = toProcess.Count(i => i.InStock);
                totalInStock += inStock;

                int withNotes = 0;
                long totalHtmlBytes = 0;
                long totalFetchMs = 0;
                long totalParseMs = 0;
                int regexHits = 0;
                int domHits = 0;
                int noisyDiscarded = 0;

                for (int idx = 0; idx < toProcess.Count; idx++)
                {
                    var item = toProcess[idx];
                    using (log.BeginScope("Item={Index}/{Total}", idx + 1, toProcess.Count))
                    using (log.BeginScope("ItemKey={Key}", item.ItemKey))
                    {
                        log.LogInformation("Item: {Title} | {Price} | {Stock} | {Url}",
                                           item.Title,
                                           item.PriceCents.HasValue ? ("$" + (item.PriceCents.Value / 100.0m).ToString("0.00")) : "(n/a)",
                                           item.InStock ? "In stock" : "Sold out",
                                           item.Url);
                        string html = string.Empty;
                        try
                        {
                            var fsw = Stopwatch.StartNew();
                            html = await http.GetStringAsync(item.Url, null, cts.Token);
                            fsw.Stop();
                            totalFetchMs += fsw.ElapsedMilliseconds;
                            totalHtmlBytes += System.Text.Encoding.UTF8.GetByteCount(html);
                        }
                        catch (Exception ex)
                        {
                            totalErrors++;
                            log.LogWarning(ex, "Failed to fetch product page");
                            continue;
                        }

                        try
                        {
                            string? notes = null;
                            string method = "none";

                            // First pass: regex/meta/fallback
                            var psw = Stopwatch.StartNew();
                            notes = NotesExtractor.ExtractNotesFromHtml(html);
                            psw.Stop();
                            totalParseMs += psw.ElapsedMilliseconds;

                            if (!string.IsNullOrWhiteSpace(notes))
                            {
                                method = "regex";
                                regexHits++;
                            }

                            // Noise filter or empty -> try DOM-based parser specific to roaster
                            if (string.IsNullOrWhiteSpace(notes) || LooksNoisy(notes))
                            {
                                if (!string.IsNullOrWhiteSpace(notes) && LooksNoisy(notes))
                                {
                                    noisyDiscarded++;
                                    log.LogInformation("Discarded noisy extraction (regex): {Snippet}", Trunc(notes, 160));
                                    notes = null;
                                }
                                var psw2 = Stopwatch.StartNew();
                                var dom = await DomNotesParser.TryExtractAsync(html, src.ParserType ?? string.Empty);
                                psw2.Stop();
                                totalParseMs += psw2.ElapsedMilliseconds;
                                if (!string.IsNullOrWhiteSpace(dom) && !LooksNoisy(dom))
                                {
                                    notes = dom;
                                    method = "dom";
                                    domHits++;
                                }
                                else if (!string.IsNullOrWhiteSpace(dom) && LooksNoisy(dom))
                                {
                                    noisyDiscarded++;
                                    log.LogInformation("Discarded noisy extraction (dom): {Snippet}", Trunc(dom, 160));
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(notes))
                            {
                                withNotes++;
                                log.LogInformation("Notes(method={Method}): {Notes}", method, notes);
                            }
                            else
                            {
                                log.LogInformation("Notes: (none)");
                            }
                        }
                        catch (Exception ex)
                        {
                            totalErrors++;
                            log.LogError(ex, "Error parsing notes");
                        }
                    }
                }

                totalProcessed += toProcess.Count;
                totalWithNotes += withNotes;
                totalRegexHits += regexHits;
                totalDomHits += domHits;
                totalNoisyDiscarded += noisyDiscarded;

                log.LogInformation("Summary for {Roaster}: processed={Processed}, inStock={InStock}, withNotes={WithNotes}, errors={Errors}, avgFetchMs={AvgFetch}, avgParseMs={AvgParse}, totalHtmlKB={KB}, regexHits={RegexHits}, domHits={DomHits}, noisyDiscarded={Noisy}",
                                   src.Name,
                                   toProcess.Count,
                                   inStock,
                                   withNotes,
                                   totalErrors,
                                   toProcess.Count > 0 ? (totalFetchMs / (double)toProcess.Count).ToString("0") : "0",
                                   toProcess.Count > 0 ? (totalParseMs / (double)toProcess.Count).ToString("0") : "0",
                                   (totalHtmlBytes / 1024),
                                   regexHits,
                                   domHits,
                                   noisyDiscarded);
            }
        }

        overallSw.Stop();
        log.LogInformation("Overall metrics: processed={TotalProcessed}, inStock={TotalInStock}, withNotes={TotalWithNotes}, errors={Errors}, elapsedMs={Elapsed}, regexHits={RegexHits}, domHits={DomHits}, noisyDiscarded={Noisy}",
                           totalProcessed, totalInStock, totalWithNotes, totalErrors, overallSw.ElapsedMilliseconds, totalRegexHits, totalDomHits, totalNoisyDiscarded);
        return 0;
    }

    private static bool LooksNoisy(string s)
    {
        var l = s.ToLowerInvariant();
        if (l.Contains("view details")) return true;
        if (l.Contains("rating of")) return true;
        if (l.Contains("the rating of this product")) return true;
        if (l.Contains("add to cart")) return true;
        if (l.Contains("select options")) return true;
        if (l.Contains("meet the countries")) return true;
        if (l.Contains("write a review")) return true;
        return false;
    }

    private static string Trunc(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max);

    private static ISiteScraper GetScraper(Source src, IHttpFetcher http)
    {
        if (string.Equals(src.ParserType, "BlackAndWhite", StringComparison.OrdinalIgnoreCase))
            return new BlackAndWhiteScraper(http);
        if (string.Equals(src.ParserType, "Brandywine", StringComparison.OrdinalIgnoreCase))
            return new BrandywineScraper(http);
        throw new InvalidOperationException("Unknown roaster parser: " + src.ParserType);
    }
}
