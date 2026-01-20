# Project Searcher - Architecture

## Overview
Project Searcher is a lightweight Windows desktop application that provides Spotlight-style instant search for project folders on the Q: drive network share. Built with C# and WPF, it runs in the background with minimal resource usage and responds to a global hotkey (Ctrl+Shift+P by default).

## Design Goals
- **Lightweight** - <50MB RAM usage when idle
- **Fast** - Search results appear in <100ms
- **Non-intrusive** - Global hotkey activation, auto-hide on focus loss
- **Native** - C# + WPF for Windows-native performance
- **Reliable** - Graceful handling of network disconnections

## Architecture Layers

### 1. Core Layer (`ProjectSearcher.Core`)
Domain models and service interfaces. No external dependencies.

**Models:**
- `Project` - Represents a project folder with number, name, path, year
- `ProjectMetadata` - User-added tags, status, location, notes, favorites
- `SearchResult` - Project with relevance score and matched fields
- `SearchFilter` - Parsed search query with prefix filters

**Abstractions:**
- `IProjectScanner` - Q: drive scanning interface
- `ISearchService` - Search and filtering interface
- `IDataStore` - Data persistence interface
- `ISettingsService` - Settings management interface

### 2. Infrastructure Layer (`ProjectSearcher.Infrastructure`)
Concrete implementations of core interfaces.

**Components:**
- `ProjectScanner` - Scans Q: drive using regex patterns
  - Old format: `2024638.001 Project Name`
  - New format: `P250784.00 - Project Name`
  - Year directories: `_Proj-24`, `_Proj-2024`
  
- `SearchService` - Fuzzy matching with Levenshtein distance
  - Prefix filter parsing (loc:, status:, year:, tag:, fav)
  - Relevance scoring and ranking
  - Debounced search (150ms delay)

- `SqliteDataStore` - SQLite database for persistence
  - Projects table with indexes on number/name/year
  - Metadata table with user-added information
  - Settings table for configuration
  - Batch upsert for performance

- `SettingsService` - JSON file-based settings
  - Stored in `%AppData%\ProjectSearcher\settings.json`
  - Q: drive path, scan interval, hotkey, theme, auto-start

### 3. UI Layer (`ProjectSearcher.UI`)
WPF-based user interface with global hotkey support.

**Components:**
- `SearchOverlay` - Main search window
  - Frameless, always-on-top, semi-transparent
  - Centered on screen, auto-hide on deactivation
  - Keyboard navigation (↑↓ arrows, Enter, Escape)
  - Real-time search with debouncing

- `GlobalHotkey` - Win32 API hotkey registration
  - Uses `RegisterHotKey` and `UnregisterHotKey`
  - Handles WM_HOTKEY messages
  - Configurable modifier keys and virtual key code

- `TrayIcon` - System tray integration
  - Context menu (Open Search, Rescan, Settings, Exit)
  - Balloon notifications
  - Double-click to open search

- `Converters` - XAML value converters
  - `BoolToVisibilityConverter` - Show/hide based on boolean
  - `NullToVisibilityConverter` - Show/hide based on null

## Data Flow

### Startup Flow
1. App launches, initializes services
2. Loads settings from JSON file
3. Initializes SQLite database (creates tables if needed)
4. Registers global hotkey (Ctrl+Shift+P)
5. Creates system tray icon
6. Loads projects from database
7. Starts background scan if needed (based on last scan time)
8. Hides window, waits for hotkey

### Search Flow
1. User presses Ctrl+Shift+P
2. Window shows with fade-in animation
3. User types search query
4. Query is debounced (150ms delay)
5. Query is parsed into filters
6. Search service filters and ranks projects
7. Results displayed in list (max 10 visible)
8. User navigates with arrow keys
9. User presses Enter to open folder
10. Window hides with fade-out animation

### Background Scan Flow
1. Check last scan time from database
2. If > scan interval (default 30 min), trigger scan
3. Scan Q: drive year directories in parallel
4. Parse project folders with regex
5. Batch upsert to database
6. Update last scan timestamp
7. Reload projects in memory

## Performance Optimizations

### Database
- Indexes on frequently queried columns (number, name, year)
- Batch upsert for bulk operations
- Connection pooling via SQLite

### Search
- In-memory project list for fast filtering
- Debounced search to avoid excessive queries
- Cancellation tokens for interrupted searches
- Levenshtein distance caching

### UI
- Virtualized list for large result sets
- Async/await for non-blocking operations
- Dispatcher.Invoke for thread-safe UI updates
- Opacity animations for smooth show/hide

### Scanning
- Parallel directory scanning
- Incremental updates (only changed directories)
- Low-priority background threads
- Cancellation token support

## Error Handling

### Q: Drive Disconnection
- Graceful fallback to cached database
- User notification via status bar
- Retry logic with exponential backoff

### Hotkey Registration Failure
- Show error message with conflicting app info
- Allow user to change hotkey in settings
- Fall back to tray icon activation

### Database Corruption
- Automatic backup before operations
- Rebuild from Q: drive scan if needed
- User notification and recovery options

## Security Considerations

### Data Privacy
- All data stored locally (no cloud)
- SQLite database in user's AppData folder
- No external network requests

### File System Access
- Read-only access to Q: drive
- No file modifications
- Respects Windows file permissions

## Extensibility

### Adding New Search Filters
1. Add property to `SearchFilter` model
2. Add regex pattern to `SearchService.ParseQuery`
3. Add filtering logic to `PassesFilters` method
4. Update UI help text

### Adding New Project Actions
1. Add keyboard shortcut handler in `SearchOverlay.Window_KeyDown`
2. Implement action method (e.g., `OpenInVSCode`)
3. Add context menu item
4. Update help text in status bar

### Adding Settings
1. Add property to `SettingsService.AppSettings`
2. Add getter/setter to `ISettingsService`
3. Create settings UI in `SettingsWindow`
4. Save/load in JSON file

## Deployment

### Build Configuration
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Output
- Single executable: `ProjectSearcher.exe`
- Size: ~15-20MB (self-contained)
- No dependencies required

### Installation
1. Copy executable to desired location
2. Run once to initialize database and settings
3. Configure Q: drive path if different
4. Optionally enable auto-start with Windows

## Future Enhancements
- Settings window for configuration
- Custom hotkey configuration UI
- Recent projects quick access
- Project templates and favorites
- Deltek API integration
- Team collaboration features
- Custom metadata fields
- Export search results
- Dark/light theme toggle
- Multi-monitor support
