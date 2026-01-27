# Project Searcher

> **⚠️ DEPRECATED - January 27, 2026**  
> This project has been migrated to **DesktopHub** - a modular widget host platform.  
> ProjectSearcher functionality is now available as a widget within DesktopHub.  
> **Please use [DesktopHub](../DesktopHub/README.md) for all new development.**

A lightweight Windows desktop application that provides **Spotlight-style instant search** for project folders on the Q: drive network share. Press `Ctrl+Alt+Space` from anywhere to instantly search and open projects.

## Key Features
- **Global Hotkey** - Press `Ctrl+Alt+Space` to instantly bring up search overlay
- **Blazing Fast** - Search results appear as you type (<100ms)
- **Lightweight** - Runs in background with <50MB RAM usage
- **Smart Search** - Fuzzy matching, prefix filters (loc:Miami; status:Active)
- **System Tray** - Runs silently in background, non-intrusive
- **Native Windows** - Built with C# + WPF for optimal performance

## Quick Start

### Prerequisites
- Windows 10/11
- .NET 8 Runtime
- Access to Q: drive network share

### Running the Application
1. Launch `ProjectSearcher.exe`
2. App minimizes to system tray
3. Press `Ctrl+Alt+Space` to open search overlay
4. Type to search projects
5. Press `Enter` to open selected project folder
6. Press `Escape` to close overlay

## Search Syntax

### Basic Search
```
2024638        # Search by project number
Palm Beach     # Search by project name
P250784        # New format project number
```

### Prefix Filters
```
loc:Miami                    # Filter by location
status:Active                # Filter by status
year:2024                    # Filter by year
tag:residential              # Filter by tag
fav                          # Show only favorites
loc:Miami; status:Active     # Combine multiple filters
```

### Keyboard Shortcuts
- `Ctrl+Alt+Space` - Open search overlay (global)
- `↑` / `↓` - Navigate results
- `Enter` - Open selected project folder
- `Ctrl+C` - Copy project path to clipboard
- `Ctrl+Shift+O` - Open in VS Code
- `Escape` - Close overlay

## Architecture

```
ProjectSearcher/
├── src/
│   ├── ProjectSearcher.Core/          # Domain models, interfaces
│   ├── ProjectSearcher.Infrastructure/ # SQLite, file scanning
│   ├── ProjectSearcher.UI/            # WPF search overlay
│   └── ProjectSearcher.DevHarness/    # Testing harness
└── tests/
    └── ProjectSearcher.Tests/         # Unit tests
```

## Tech Stack
- **Language:** C# 12 on .NET 8
- **UI:** WPF (Windows Presentation Foundation)
- **Database:** SQLite (Microsoft.Data.Sqlite)
- **Hotkey:** Win32 API via P/Invoke
- **Testing:** xUnit

## System Tray Menu
- **Open Search** - Show search overlay
- **Rescan Projects** - Force refresh from Q: drive
- **Settings** - Configure hotkey, scan interval, theme
- **Exit** - Close application

## Settings
- **Global Hotkey** - Customize activation key combination
- **Q: Drive Path** - Configure network share path
- **Scan Interval** - How often to refresh project list
- **Theme** - Light or dark mode
- **Auto-start** - Launch with Windows

## Performance
- Handles 1000+ projects efficiently
- Search results in <100ms
- Background scanning doesn't block UI
- Minimal memory footprint (<50MB idle)
- Incremental updates (only scans changed directories)

## Privacy
- All data stored locally in SQLite
- No cloud services or external connections
- Project metadata stays on your machine

## Building from Source

### Requirements
- .NET 8 SDK
- Visual Studio 2022 or VS Code with C# extension

### Build Commands
```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run application
dotnet run --project src/ProjectSearcher.UI

# Run tests
dotnet test

# Publish self-contained executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Troubleshooting

### Search overlay doesn't appear
- Check if hotkey is already registered by another app
- Try changing hotkey in Settings
- Restart application

### Q: drive not found
- Verify Q: drive is mounted in File Explorer
- Check Settings → Q: Drive Path
- Ensure network connection is active

### Slow search results
- Run "Rescan Projects" from tray menu
- Check if Q: drive connection is slow
- Reduce scan interval in Settings

## Roadmap
- [ ] Integration with Deltek API
- [ ] Recent projects quick access
- [ ] Project templates and favorites
- [ ] Team collaboration features
- [ ] Custom metadata fields
- [ ] Export search results

## License
Internal tool for company use.

---

**Version:** 1.0.0  
**Last Updated:** 2026-01-14
