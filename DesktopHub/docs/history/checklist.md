# DesktopHub - Development Checklist

> Status: historical planning checklist (retained for reference).
> Canonical docs index: `../README.md`.

## Core Concept
Create a **lightweight background Windows application** that provides **Spotlight-style instant search** for project folders on the Q: drive network share. Users press a global hotkey (e.g., `Ctrl+Alt+Space`) to instantly bring up a search overlay, type to filter projects, and press Enter to open the folder. The app runs silently in the system tray with minimal memory footprint.

**Key Design Goals:**
- **Lightweight** - Runs in background with <50MB RAM usage
- **Fast** - Search results appear as you type (<100ms)
- **Non-intrusive** - Global hotkey activation, auto-hide on focus loss
- **Native** - C# + WPF for Windows-native performance

## Architecture (Following coffee-stock-widget pattern)

### Project Structure
```
DesktopHub/
├── README.md
├── DesktopHub.sln
├── docs/
│   ├── ARCHITECTURE.md
│   ├── HOTKEY_GUIDE.md
│   └── SEARCH_SYNTAX.md
├── src/
│   ├── DesktopHub.Core/          # Domain models, services
│   ├── DesktopHub.Infrastructure/ # SQLite, file scanning
│   ├── DesktopHub.UI/            # WPF search overlay
│   └── DesktopHub.DevHarness/    # Testing harness
└── tests/
    └── DesktopHub.Tests/
```

## Key Features to Implement

### ✅ Phase 1: Core Infrastructure
- [ ] **Solution Setup**
  - [ ] Create .NET 8 solution with 4 projects (Core, Infrastructure, UI, Tests)
  - [ ] Configure project references and NuGet packages
  - [ ] Set up SQLite with Microsoft.Data.Sqlite
  - [ ] Create database schema for projects and metadata

- [ ] **Q-Drive Scanner Service**
  - [ ] `IProjectScanner` interface
  - [ ] Regex patterns for old format (2024638.001 Name)
  - [ ] Regex patterns for new format (P250784.00 - Name)
  - [ ] Background scanning with cancellation support
  - [ ] Cache results with 5-minute TTL
  - [ ] Incremental updates (only scan changed directories)

- [ ] **Data Models**
  - [ ] `Project` - id, fullNumber, shortNumber, name, path, year
  - [ ] `ProjectMetadata` - tags, status, location, notes, isFavorite
  - [ ] `SearchResult` - project + relevance score

### ✅ Phase 2: Search & Filter Engine
- [ ] **Search Service**
  - [ ] `ISearchService` interface
  - [ ] Fuzzy matching algorithm (Levenshtein distance)
  - [ ] Prefix-based filter parser (loc:Miami; status:Active)
  - [ ] Search result ranking by relevance
  - [ ] Search history persistence

- [ ] **Filter Syntax Support**
  - [ ] `loc:` or `location:` - filter by location
  - [ ] `status:` - filter by status
  - [ ] `year:` - filter by year
  - [ ] `tag:` - filter by tags
  - [ ] `fav` or `favorite` - show only favorites
  - [ ] Plain text - fuzzy match on name/number

### ✅ Phase 3: UI - Search Overlay (Spotlight-style)
- [ ] **Global Hotkey Registration**
  - [ ] Register `Ctrl+Alt+Space` using Win32 API
  - [ ] Handle hotkey in background thread
  - [ ] Show/hide overlay on hotkey press

- [ ] **Search Overlay Window**
  - [ ] Centered, always-on-top, frameless WPF window
  - [ ] Semi-transparent dark background overlay
  - [ ] Rounded search box with modern styling
  - [ ] Real-time search as user types
  - [ ] Keyboard navigation (Up/Down arrows, Enter to open)
  - [ ] Auto-hide on focus loss or Escape key

- [ ] **Results Display**
  - [ ] List view with project number, name, location
  - [ ] Highlight matching text
  - [ ] Show project path on hover
  - [ ] Icon indicators (favorite, recent, etc.)
  - [ ] Max 10 results visible, scroll for more

### ✅ Phase 4: System Tray Integration
- [ ] **Tray Icon**
  - [ ] System tray icon with context menu
  - [ ] Menu: Open Search, Rescan Projects, Settings, Exit
  - [ ] Show notification on first run
  - [ ] Auto-start with Windows (optional)

- [ ] **Background Service**
  - [ ] Run scanner on startup (async)
  - [ ] Periodic background rescans (configurable interval)
  - [ ] Monitor Q: drive for changes (FileSystemWatcher)
  - [ ] Low-priority background threads

### ✅ Phase 5: Project Actions
- [ ] **Primary Actions**
  - [ ] Open folder in Explorer (Enter key)
  - [ ] Copy path to clipboard (Ctrl+C)
  - [ ] Open in VS Code (Ctrl+Shift+O)
  - [ ] Show in Deltek (if integrated)

- [ ] **Metadata Management**
  - [ ] Right-click context menu on results
  - [ ] Quick-add tags
  - [ ] Toggle favorite status
  - [ ] Edit location/status
  - [ ] Add notes

### ✅ Phase 6: Settings & Polish
- [ ] **Settings Window**
  - [ ] Configure global hotkey
  - [ ] Set Q: drive path
  - [ ] Configure scan interval
  - [ ] Theme selection (light/dark)
  - [ ] Auto-start toggle

- [ ] **Performance & Reliability**
  - [ ] Crash logging to file
  - [ ] Graceful handling of Q: drive disconnection
  - [ ] Memory usage monitoring
  - [ ] Startup performance optimization

## Technical Stack
- **Language:** C# 12 on .NET 8
- **UI:** WPF (Windows Presentation Foundation)
- **Database:** SQLite (Microsoft.Data.Sqlite)
- **Hotkey:** Win32 API via P/Invoke
- **Testing:** xUnit
- **Packaging:** Self-contained single-file publish

## Success Criteria
- ✅ App uses <50MB RAM when idle
- ✅ Search results appear in <100ms
- ✅ Global hotkey works from any application
- ✅ Handles 1000+ projects without lag
- ✅ Overlay shows/hides smoothly (<200ms animation)
- ✅ Fuzzy matching finds projects with typos
- ✅ No UI freezing during background scans
