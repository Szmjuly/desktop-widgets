# Tech Choices

## Primary recommendation: C#/.NET 8 + WPF
- Native Windows experience, low memory footprint, excellent toast notifications.
- Mature desktop UI toolkit, easy always-on-top windows and tray icons.
- Rich ecosystem for HTTP and HTML parsing (HttpClient, AngleSharp, HtmlAgilityPack).
- Easy packaging as a single-file, self-contained executable.

## Alternatives considered
- Tauri (Rust + WebView): smaller than Electron, good cross-platform; Windows toasts doable via plugins. Trade-off: more moving parts (Rust toolchain, web UI), and WebView2 dependency.
- WinUI 3: modern, but ecosystem still stabilizing; toast story similar; heavier tooling.
- Electron: fast to iterate, but heavy memory/CPU for a tiny widget.
- Python (PySide/PyQt/wx): quick to prototype, but packaging/notifications less native; scraping/libs are fine, but long-running stability not as strong as .NET for this case.

## Libraries/Packages (tentative)
- Notifications: `Microsoft.Toolkit.Uwp.Notifications`
- HTML: `AngleSharp` (or `HtmlAgilityPack`), pick one; AngleSharp has CSS selector support.
- SQLite: `Microsoft.Data.Sqlite` (or `Dapper` + raw SQL); migrations: `FluentMigrator` or manual.
- DI/Hosting: `.NET Generic Host` with `Microsoft.Extensions.DependencyInjection` and `Configuration`.
- Logging: `Serilog` or built-in `ILogger` + file sink.

## Why not a browser automation engine by default?
- Many sites are static or render product listings server-side. Start with HTTP + HTML parsing to keep footprint tiny.
- If a site demands JS execution, add an optional integration (e.g., Playwright) behind a feature flag.
