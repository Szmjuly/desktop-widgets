# Roadmap

## Phase 1 — Foundations
- Decide tech stack (C#/.NET 8 + WPF)
- Scaffold solution and projects (UI, Core, Scraping, Infrastructure, Tests)
- Implement SQLite schema and migrations
- Implement Scheduler and HttpFetcher with rate limiting

## Phase 2 — First Site & Notifications
- Black & White Roasters scraper
- Change detection and normalization
- Windows toast notifications + widget bubble

## Phase 3 — UX & Settings
- Minimal widget UI + tray controls
- Settings UI and JSON-backed config
- Retention policy and pruning jobs

## Phase 4 — Generic Crawler & Extensibility
- Heuristic-based generic crawler
- Dictionaries for profile extraction
- Add 1–2 additional roasters

## Phase 5 — Polish & Packaging
- Logging/telemetry (local)
- Self-contained publish; optional auto-start
- Resource budget validation (<50MB RAM idle, low CPU)
