# Project Tagging System — Architecture

## Overview

Structured tagging system for engineering projects. Tags are stored in Firebase RTDB with HMAC-SHA256 hashed project keys so Firebase never sees plaintext project identifiers. A local JSON cache enables instant search without network round-trips.

## Security Model

| Layer | Protection |
|---|---|
| **Firebase key** | HMAC-SHA256 hash of project number — not reversible without the secret |
| **HMAC secret** | 256-bit key stored locally at `%LOCALAPPDATA%/DesktopHub/tag_secret.key` |
| **Firebase rules** | `project_tags/$hash` → `.read: false, .write: false` (service account bypasses) |
| **Tag values** | Stored as plaintext (generic engineering data, not directly identifying) |

The HMAC secret must be shared across machines that need to read/write the same tags. Use `tag-manager.ps1 -Action show-secret` to export the Base64 secret, then copy the key file to the same path on other machines.

## Firebase RTDB Schema

```
project_tags/
  {hmac_hash}/
    tags/
      voltage: "208"
      phase: "3"
      amperage_service: "400"
      hvac_type: "DX"
      hvac_brand: "Carrier"
      ...
      custom/
        my_field: "my_value"
    updated_by: "smarkowitz"
    updated_at: "2025-03-05T17:00:00Z"
```

## Tag Fields

Defined in `TagFieldRegistry.cs`. Each field has a canonical key, display name, aliases, suggested values, and category.

| Category | Fields |
|---|---|
| **Electrical** | voltage, phase, amperage_service, amperage_generator, generator_brand, generator_load_kw |
| **Mechanical** | hvac_type, hvac_brand, hvac_tonnage, hvac_load_kw |
| **Building** | square_footage, build_type |
| **Location** | location_city, location_state, location_municipality, location_address |
| **People** | stamping_engineer, engineers |
| **Code** | code_refs |
| **Custom** | Any key=value pair via the `custom` sub-node |

## Search Syntax

Tag search uses **single colon** `:` as the delimiter (since `::` is taken by SmartProjectSearch file type suffixes).

| Query | Meaning |
|---|---|
| `voltage:208` | Projects with voltage = 208 |
| `hvac:DX` | HVAC type = DX |
| `gen:Generac` | Generator brand = Generac (shorthand alias) |
| `v:208; phase:3` | Voltage 208 AND 3-phase (`;` separates filters) |
| `eng:Smith` | Stamping engineer contains "Smith" |
| `build:new` | New construction |
| `Boca West; volt:208` | Text search "Boca West" + voltage filter |

### Shorthand Aliases

Common aliases are resolved automatically:

| Alias | Resolves To |
|---|---|
| `v`, `volt`, `volts` | voltage |
| `ph` | phase |
| `amp`, `amps` | amperage_service |
| `gen` | generator_brand |
| `hvac` | hvac_type |
| `ton`, `tons` | hvac_tonnage |
| `sqft`, `sf` | square_footage |
| `build` | build_type |
| `eng`, `stamp`, `pe` | stamping_engineer |
| `code`, `codes` | code_refs |
| `city` | location_city |
| `state` | location_state |
| `muni` | location_municipality |

## File Layout

```
DesktopHub.Core/
  Abstractions/
    IProjectTagService.cs        — Service interface
  Models/
    ProjectTags.cs               — Tag data model + TagFieldDefinition
    TagFieldRegistry.cs          — Central alias/field registry
    SearchFilter.cs              — Extended with TagFilters list

DesktopHub.Infrastructure/
  Firebase/
    ProjectTagService.cs         — Firebase CRUD + local JSON cache
    Utilities/
      ProjectHasher.cs           — HMAC-SHA256 hashing
  Search/
    SearchService.cs             — Extended with tag filter parsing + matching

DesktopHub.UI/
  Overlays/SearchOverlay/
    SearchOverlay.xaml.cs         — _tagService field
    SearchOverlay.Initialization.cs — Service wiring
    SearchOverlay.SearchData.cs   — HasTags on ProjectViewModel
    SearchOverlay.ResultsInteraction.cs — Tags context menu + edit dialog

scripts/
  tag-manager.ps1                — CLI for bulk tag operations

firebase-rules-production.json   — project_tags node added
```

## Side Script Usage

```powershell
# View tags
.\scripts\tag-manager.ps1 -Action get -ProjectNumber "2024337.00"

# Set a tag
.\scripts\tag-manager.ps1 -Action set -ProjectNumber "2024337.00" -TagKey voltage -TagValue 208

# Bulk import from CSV (column headers = field keys, must have project_number column)
.\scripts\tag-manager.ps1 -Action import -CsvFile tags.csv

# Export all tags
.\scripts\tag-manager.ps1 -Action export -CsvFile tags_export.csv

# Show HMAC secret for sharing
.\scripts\tag-manager.ps1 -Action show-secret

# List all tagged projects (hash + count)
.\scripts\tag-manager.ps1 -Action list
```

## UX Features

### Tag Carousel (Mode 1 — default)

Horizontal scrolling chip bar below the search bar, above the history pills. Shows the most frequently-used tag values across all cached projects. Clicking a chip injects `key:value` into the search bar and adds it to search history.

- **Position**: Grid Row 1 in SearchOverlay (between search bar and history)
- **Populated from**: `ProjectTagService.GetAllCachedTags()` — frequency-sorted
- **Max chips**: Configurable in Settings → Tags (default 8, range 3-20)
- **Auto-refresh**: On tag save, on app launch after cache load

### Settings Panel (Tags Section)

Dedicated "🏷️ Tags" nav item in the Settings window with:

| Setting | Default | Description |
|---|---|---|
| Tag Search | On | Enable `key:value` search filters |
| Display Mode | Carousel | "Carousel" or "Off" |
| Max Carousel Chips | 8 | 3-20 |
| Auto-Refresh Carousel | On | Refresh chips on tag create/update |

Also shows search syntax reference card.

### Telemetry & Metrics

Tag events tracked via `TelemetryCategory.Tag`:

| Event | Fired When |
|---|---|
| `tag_created` | First tags saved for a project |
| `tag_updated` | Existing tags modified |
| `tag_search_executed` | Tag filter used in search query |
| `tag_carousel_clicked` | Carousel chip clicked |

`DailyMetricsSummary` extended with: `TotalTagsCreated`, `TotalTagsUpdated`, `TotalTagSearches`, `TotalTagCarouselClicks`.

Personal metrics viewer activity card shows all four tag metrics. Admin multi-user view inherits the same fields via Firebase sync.

### Tag Search History

Tag queries (`voltage:208`, `gen:Generac`) are added to search history chips via `AddToSearchHistory()` when a carousel chip is clicked or a tag search is executed.

## Local Cache

File: `%LOCALAPPDATA%/DesktopHub/tag_cache.json`

Loaded on app startup, synced from Firebase in the background. Search queries hit the cache for instant results.

```json
{
  "LastSynced": "2025-03-05T17:00:00Z",
  "Entries": [
    {
      "ProjectNumber": "2024337.00",
      "Hash": "a7f3b2c1d4e5...",
      "Tags": {
        "Voltage": "208",
        "Phase": "3",
        ...
      }
    }
  ]
}
```
