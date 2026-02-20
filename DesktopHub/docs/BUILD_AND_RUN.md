# Build and Run Guide

> Docs index: `README.md`

## Prerequisites

### Required
- Windows 10/11 (64-bit)
- .NET 8 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Access to Q: drive network share

### Optional (for development)
- Visual Studio 2022 or VS Code with C# extension
- Git for version control

## Building from Source

### 1. Clone Repository
```bash
git clone <repository-url>
cd DesktopHub
```

### 2. Restore Dependencies
```bash
dotnet restore
```

### 3. Build Solution
```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release
```

### 4. Run Tests
```bash
dotnet test
```

## Running the Application

### Development Mode
```bash
# Run from source
dotnet run --project src/DesktopHub.UI

# Or with hot reload
dotnet watch run --project src/DesktopHub.UI
```

### Release Mode
```bash
# Build and run
dotnet build -c Release
dotnet run --project src/DesktopHub.UI -c Release
```

## Publishing

### Self-Contained Executable
Creates a single executable with all dependencies included:

```bash
dotnet publish src/DesktopHub.UI -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true
```

Output: `src/DesktopHub.UI/bin/Release/net8.0-windows/win-x64/publish/DesktopHub.exe`

### Framework-Dependent Executable
Smaller size but requires .NET 8 runtime installed:

```bash
dotnet publish src/DesktopHub.UI -c Release -r win-x64 \
  --self-contained false \
  -p:PublishSingleFile=true
```

## First Run Setup

### 1. Launch Application
- Double-click `DesktopHub.exe`
- App will minimize to system tray
- Balloon notification shows hotkey (Ctrl+Alt+Space)

### 2. Initial Scan
- First launch triggers Q: drive scan
- May take 30-60 seconds depending on project count
- Progress shown in status bar when overlay is open

### 3. Configure Settings (Optional)
- Right-click tray icon → Settings
- Configure Q: drive path if different from `Q:\`
- Adjust scan interval (default: 30 minutes)
- Change global hotkey if desired
- Enable auto-start with Windows

## Usage

### Opening Search
- Press `Ctrl+Alt+Space` from any application
- Or double-click system tray icon
- Or right-click tray icon → Open Search

### Searching Projects
```
# Simple search
2024638
Palm Beach

# With filters
loc:Miami
status:Active
year:2024

# Combined
palm beach; loc:Miami; status:Active
```

### Keyboard Shortcuts
- `↑` / `↓` - Navigate results
- `Enter` - Open selected project folder
- `Ctrl+C` - Copy project path to clipboard
- `Escape` - Close search overlay

### System Tray Menu
- **Open Search** - Show search overlay
- **Rescan Projects** - Force refresh from Q: drive
- **Settings** - Open settings window
- **Exit** - Close application

## Troubleshooting

### Hotkey Not Working
**Problem:** Ctrl+Alt+Space doesn't show overlay

**Solutions:**
1. Check if another app is using the hotkey
2. Try changing hotkey in Settings
3. Restart application
4. Use tray icon as fallback

### Q: Drive Not Found
**Problem:** "Q: drive not found" error

**Solutions:**
1. Verify Q: drive is mounted in File Explorer
2. Check Settings → Q: Drive Path
3. Ensure network connection is active
4. Try mapping Q: drive manually

### Slow Search Results
**Problem:** Search takes >1 second

**Solutions:**
1. Run "Rescan Projects" from tray menu
2. Check Q: drive connection speed
3. Reduce scan interval in Settings
4. Clear database and rescan

### Application Won't Start
**Problem:** Double-clicking exe does nothing

**Solutions:**
1. Check if .NET 8 runtime is installed (for framework-dependent builds)
2. Look for `Crash.log` in application folder
3. Run from command line to see error messages
4. Check Windows Event Viewer for errors

### High Memory Usage
**Problem:** App using >100MB RAM

**Solutions:**
1. Check number of projects (>10,000 may use more memory)
2. Restart application
3. Clear database and rescan
4. Report issue with project count

## Development Tips

### Hot Reload
Use `dotnet watch` for automatic recompilation:
```bash
dotnet watch run --project src/DesktopHub.UI
```

### Debugging
1. Open solution in Visual Studio
2. Set `DesktopHub.UI` as startup project
3. Press F5 to debug
4. Breakpoints work in all projects

### Testing Individual Components
```bash
# Test scanner only
dotnet test --filter "FullyQualifiedName~ScannerTests"

# Test search service only
dotnet test --filter "FullyQualifiedName~SearchServiceTests"
```

### Database Inspection
SQLite database location:
```
%AppData%\DesktopHub\projects.db
```

Use [DB Browser for SQLite](https://sqlitebrowser.org/) to inspect.

### Settings File
Settings location:
```
%AppData%\DesktopHub\settings.json
```

Edit manually if needed (app must be closed).

## Performance Benchmarks

### Expected Performance
- **Startup time:** <2 seconds
- **Search response:** <100ms for 1000 projects
- **Q: drive scan:** 30-60 seconds for 1000 projects
- **Memory usage:** 30-50MB idle, 60-80MB during search
- **Disk usage:** ~5MB (database + settings)

### Optimization Tips
1. Increase scan interval if Q: drive is slow
2. Use SSD for AppData folder
3. Close other apps using Q: drive
4. Ensure good network connection to Q: drive

## Auto-Start with Windows

### Enable via Settings
1. Right-click tray icon → Settings
2. Check "Auto-start with Windows"
3. Click Save

### Manual Setup
1. Press `Win+R`
2. Type `shell:startup` and press Enter
3. Create shortcut to `DesktopHub.exe`
4. Restart Windows to test

### Disable Auto-Start
1. Press `Win+R`
2. Type `shell:startup` and press Enter
3. Delete `DesktopHub` shortcut

## Uninstallation

### Remove Application
1. Close application (right-click tray icon → Exit)
2. Delete `DesktopHub.exe`
3. Remove auto-start shortcut if created

### Remove Data (Optional)
Delete folder:
```
%AppData%\DesktopHub
```

This removes:
- SQLite database
- Settings file
- Any cached data

## Support

### Reporting Issues
Include in bug reports:
1. Windows version
2. .NET version (`dotnet --version`)
3. Number of projects scanned
4. Steps to reproduce
5. `Crash.log` if available
6. Screenshot if UI-related

### Feature Requests
Submit with:
1. Use case description
2. Expected behavior
3. Current workaround (if any)
4. Priority (nice-to-have vs. critical)
