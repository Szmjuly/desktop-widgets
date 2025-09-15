# Function Plan

## Interfaces
- `IScheduler`
  - `StartAsync()`, `StopAsync()`, `Register(Source)`, `UpdateSchedule(Source)`
- `ISiteScraper`
  - `Task<IReadOnlyList<CoffeeItem>> FetchAsync(Source source, CancellationToken ct)`
- `IGenericCrawler`
  - `Task<IReadOnlyList<CoffeeItem>> CrawlAsync(Uri root, CancellationToken ct)`
- `IProfileExtractor`
  - `CoffeeProfile BuildProfile(HtmlDocument doc | string html, IDictionary<string,string[]> dictionaries)`
- `IDataStore`
  - `UpsertItemsAsync(IEnumerable<CoffeeItem>)`
  - `GetItemsBySourceAsync(Source)`
  - `RecordEventsAsync(IEnumerable<StockChangeEvent>)`
  - `PruneAsync(RetentionPolicy policy)`
- `ISettingsService`
  - `Load()`, `Save(Settings)`; watch for changes
- `INotificationService`
  - `ShowToast(StockChangeEvent)`, `ShowInWidgetBubble(StockChangeEvent)`
- `IHttpFetcher`
  - `Task<string> GetStringAsync(Uri, headers?, ct)` with per-domain rate limiting

## Core Classes
- `SchedulerService : IScheduler`
  - Priority queue of sources; applies jitter/backoff; signals workers
- `ChangeDetector`
  - Compares current snapshot vs last snapshot, outputs `StockChangeEvent` list
- `Normalization`
  - Trims titles, normalizes units/prices, generates stable `ItemKey` (SKU/hash)

## Scrapers
- `BlackAndWhiteScraper : ISiteScraper`
  - Target product listing and product pages; selectors configurable
- `GenericCrawler : IGenericCrawler`
  - BFS/limited depth; product heuristics; respects rate limiting and robots (optional)

## Infrastructure
- `SqliteDataStore : IDataStore`
  - Migrations on startup; parameterized SQL; indices for lookups
- `SettingsService`
  - JSON in `%AppData%/CoffeeStockWidget/settings.json`
- `ToastNotificationService : INotificationService`
  - AppUserModelID setup; dedupe queue
- `HttpFetcher : IHttpFetcher`
  - HttpClient per-host throttling; ETag/Last-Modified cache

## UI
- `WidgetWindow` (WPF)
  - Compact UI, always on top; shows last change and bubble
- `TrayIconService`
  - Pause/Resume, Settings, Exit
- `SettingsWindow`
  - Configure sources, intervals, limits, dictionaries

## Key Algorithms
- Stable Item Key
  - Prefer explicit SKU; otherwise hash of normalized title + weight + URL path
- Change Detection
  - Transition: out-of-stock -> in-stock, price delta, new item
- Retention
  - Keep last N items per source and last M days of events; prune on startup and hourly
