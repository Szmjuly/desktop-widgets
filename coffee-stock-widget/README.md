# Coffee Stock Widget

A lightweight Windows desktop widget that monitors coffee roaster websites for product stock in near real-time and notifies you via Windows toasts and a small in-widget bubble. First target: Black & White Roasters; designed to be extensible for additional roasters and arbitrary sites.

## Key Features (planned)
- Always-on-top compact widget with system tray presence
- Windows toast notifications + subtle in-widget bubble for new stock
- Site-specific parsers (e.g., Black & White Roasters) + generic crawler fallback
- Configurable polling intervals with jitter/backoff; per-domain rate limiting
- Local storage via SQLite with strict retention (size/time bounded)
- Coffee profile extraction (producer, origin, taste notes) via rule/dictionary-based parsing
- Extensible plugin-style architecture for adding new sites and heuristics

## Tech Stack
- Language: C# on .NET 8
- UI: WPF (Windows Presentation Foundation)
- Notifications: Windows Toast Notifications (Microsoft.Toolkit.Uwp.Notifications)
- HTML Parsing: AngleSharp / HtmlAgilityPack
- HTTP: HttpClient with handler-based rate limiting
- Storage: SQLite (Microsoft.Data.Sqlite), migrations TBD
- Testing: xUnit

Why .NET + WPF? It’s native, performant, low-memory, has first-class Windows notifications, and produces small self-contained builds. See `docs/TECH_CHOICES.md`.

## Repo Structure
```
coffee-stock-widget/
  README.md
  docs/
    ARCHITECTURE.md
    FUNCTION_PLAN.md
    DATA_MODEL.md
    TECH_CHOICES.md
    ROADMAP.md
  src/
    .gitkeep
  tests/
    .gitkeep
```

## Getting Started
- Status: documentation and planning complete; scaffolding next.
- Next step: scaffold .NET solution and projects (`src/`), then implement the first site parser (Black & White Roasters).

## Privacy & Ethics
- All data is stored locally. No cloud involved.
- Site access is rate-limited and respectful. You can opt to only parse known product endpoints.

## Roadmap
See `docs/ROADMAP.md`.
