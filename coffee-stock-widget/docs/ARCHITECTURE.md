# Architecture

## Goals
- Lightweight, low-CPU, low-memory, native Windows experience
- Accurate change detection for product stock
- Extensible to new roasters/sites with minimal code changes
- Local-only data with bounded storage

## Components
- UI (WPF Widget + Tray)
  - Compact always-on-top window, shows current status and a subtle bubble on new stock
  - System tray menu (pause/resume, settings, exit)
- Core
  - Domain models: `CoffeeItem`, `StockStatus`, `CoffeeProfile`, `Source`, `StockChangeEvent`
  - Change detection and deduplication
  - Scheduler with jitter/backoff and per-domain rate limiting
- Scraping
  - `ISiteScraper` for site-specific logic (e.g., Black & White)
  - `IGenericCrawler` fallback using heuristics and dictionaries
  - Normalization pipeline -> `CoffeeItem` list
- Infrastructure
  - Storage (`IDataStore`) via SQLite
  - Settings (`ISettingsService`) stored in `%AppData%/CoffeeStockWidget/settings.json`
  - Logging
- Notifications
  - Windows Toast via `Microsoft.Toolkit.Uwp.Notifications`
  - In-widget bubble and event queue

## Data Flow
1) Scheduler picks which source to poll next (honors backoff and rate limits).
2) Scraper fetches HTML (or uses crawler) and produces normalized `CoffeeItem` objects.
3) Core compares with last known state in SQLite to detect changes (in-stock transitions, new items, price changes).
4) Changes are persisted as events and surfaced as notifications (toast + widget bubble).
5) Retention policy periodically prunes old rows to maintain a small on-disk footprint.

## Extensibility
- Add a new scraper by implementing `ISiteScraper` and registering it.
- The generic crawler covers unknown sites; accuracy may be lower but improves with curated dictionaries.
- Dictionaries for metadata extraction (producer, origin, process, tasting notes) are configurable.

## Resilience & Politeness
- Jittered intervals and exponential backoff on errors
- Per-domain concurrency = 1 and min-delay between requests
- Cache ETags/Last-Modified where available to limit bandwidth
- Optional robots.txt check in crawler mode

## Notifications
- Toast requires an App User Model ID and a Start Menu shortcut creation on first run.
- Deduplicate notifications via event IDs; keep a small queue to avoid spamming.

## Packaging
- Self-contained, single-file publish; optional auto-start via registry or Startup folder.
