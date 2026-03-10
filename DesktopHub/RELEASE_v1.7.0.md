# DesktopHub v1.7.0

## What's New

### Project Tagging System
- **Tag projects** with 20+ structured fields — voltage, phase, HVAC type, generator, location, square footage, code references, and more
- **Tag Search** — filter results using `key:value` syntax (e.g. `voltage:208`, `gen:Generac`)
- **Shared Vocabulary** — dropdown values sync across all users via Firebase so the whole team benefits from every tag added
- **Project Info panel** — structured editing with dropdowns, free-text, and multi-select fields per tag category

### Tag Carousel
- Horizontal scrolling chips appear below the search bar showing the most-used tag values
- Click any chip to instantly filter projects by that tag
- Configurable in Settings: max chips, display mode, auto-refresh

### GEC Parallel Conductor Calculator
- Built into the Cheat Sheet widget on the GEC Sizing sheet
- Select conductor material (Cu/Al), conductor size, and number of parallel sets
- Calculates total circular mil area and looks up NEC Table 250.66 to determine the correct GEC size

### Admin & Infrastructure
- **Runtime admin detection** — Firebase-based admin user check replaces compile-time build flag
- **Consolidated Firebase security rules** with service account authentication for admin scripts
- **Metrics sync** — tag usage metrics tracked and synced to a dedicated Firebase node
- **Telemetry** — tag created/updated/deleted/searched/carousel-clicked events

### Bug Fixes & Improvements
- Fixed tag carousel and search history chip duplication — tag searches no longer double-populate both areas
- Improved Project Info field row spacing for better readability
- Scripts reorganized: `admin.ps1` master console, `tag-manager.ps1`, auth cleanup, database dump, device wipe utilities archived to `_Archive/`

## Download

| Asset | Size |
|-------|------|
| `DesktopHub.exe` | ~184 MB |

> Single-file, self-contained .NET 8 executable — no installation required. Just download and run.

## Upgrade Notes

- The app will auto-detect the new version via Firebase and prompt to update
- Tag data is stored in Firebase and shared across all users automatically
- No breaking changes from v1.6.1
