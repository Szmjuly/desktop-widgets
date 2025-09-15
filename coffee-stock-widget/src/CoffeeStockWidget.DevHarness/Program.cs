using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoffeeStockWidget.Core.Models;
using CoffeeStockWidget.Core.Services;
using CoffeeStockWidget.Infrastructure.Net;
using CoffeeStockWidget.Infrastructure.Storage;
using CoffeeStockWidget.Scraping.BlackAndWhite;

// Dev harness to test the Black & White Roasters scraper quickly.

var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var http = new HttpFetcher(minDelayPerHostMs: 1200);
var scraper = new BlackAndWhiteScraper(http);
var store = new SqliteDataStore();
var detector = new ChangeDetector();

var source = new Source
{
    Id = 1,
    Name = "Black & White Roasters",
    RootUrl = new Uri("https://www.blackwhiteroasters.com/collections/all-coffee"),
    ParserType = "BlackAndWhite",
    PollIntervalSeconds = 300,
    Enabled = true
};

Console.WriteLine($"Fetching from {source.RootUrl} ...\n");
await store.UpsertSourceAsync(source, cts.Token);
var previous = await store.GetItemsBySourceAsync(source.Id.Value, cts.Token);
var items = await scraper.FetchAsync(source, cts.Token);

Console.WriteLine($"Found {items.Count} items\n");
Console.WriteLine("Detecting changes vs previously stored snapshot...\n");
var events = detector.Compare(previous, items);
foreach (var e in events)
{
    Console.WriteLine($"Event: {e.EventType} for ItemId={e.ItemId} SourceId={e.SourceId}");
}

Console.WriteLine("\nPersisting items snapshot...\n");
await store.UpsertItemsAsync(items, cts.Token);

foreach (var item in items.OrderBy(i => i.Title))
{
    var price = item.PriceCents.HasValue ? ($"$" + (item.PriceCents.Value / 100.0m).ToString("0.00")) : "(no price)";
    Console.WriteLine($"- {item.Title} | {(item.InStock ? "In Stock" : "Sold Out")} | {price} | {item.Url}");
}

Console.WriteLine("\nDone.");
