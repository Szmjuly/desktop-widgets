# Quick Start Guide

## First Time Setup

### 1. Run the Application
Open PowerShell in the project directory and run:

```powershell
.\run
```

This will:
- Download and install .NET 8 SDK locally (first time only, ~2-5 minutes)
- Build the solution
- Launch the application

### 2. What Happens on First Launch
- Application minimizes to system tray (look for icon in bottom-right)
- A balloon notification appears: "Press Ctrl+Shift+P to search projects"
- Background scan of Q: drive begins (may take 30-60 seconds)

### 3. Try the Search
Press **`Ctrl+Shift+P`** from anywhere to open the search overlay.

## Basic Usage

### Opening Search
- **Hotkey**: `Ctrl+Shift+P` (works from any application)
- **Tray Icon**: Double-click the system tray icon
- **Context Menu**: Right-click tray icon → "Open Search"

### Search Examples

**Simple search:**
```
2024638          # Find by project number
Palm Beach       # Find by project name
P250784          # New format project number
```

**With filters:**
```
loc:Miami                    # Projects in Miami
status:Active                # Active projects only
year:2024                    # 2024 projects
loc:Miami; status:Active     # Combined filters
palm beach; loc:Miami        # Text search + filter
```

### Keyboard Shortcuts
- `↑` / `↓` - Navigate through results
- `Enter` - Open selected project folder in Explorer
- `Ctrl+C` - Copy project path to clipboard
- `Escape` - Close search overlay

## System Tray Menu

Right-click the tray icon for:
- **Open Search** - Show search overlay
- **Rescan Projects** - Force refresh from Q: drive
- **Settings** - Configure application (coming soon)
- **Exit** - Close application

## Configuration

### Settings Location
Settings are stored in:
```
%AppData%\ProjectSearcher\settings.json
```

### Database Location
Project cache is stored in:
```
%AppData%\ProjectSearcher\projects.db
```

### Default Settings
- **Q: Drive Path**: `Q:\`
- **Scan Interval**: 30 minutes
- **Hotkey**: `Ctrl+Shift+P`
- **Theme**: Dark

## Troubleshooting

### "Q: drive not found"
1. Verify Q: drive is mounted in File Explorer
2. Check if you have network access
3. Try manually mapping the drive

### Hotkey doesn't work
1. Check if another app is using `Ctrl+Shift+P`
2. Try closing other apps and restarting Project Searcher
3. Use tray icon as fallback

### No results found
1. Wait for initial scan to complete (check status bar)
2. Right-click tray icon → "Rescan Projects"
3. Check Q: drive path in settings

### Application won't start
1. Check if `.dotnet` folder exists (should be created by `run` script)
2. Try deleting `.dotnet` and running again
3. Check `Crash.log` in application folder

## Development

### Running from Source
```powershell
.\run
```

### Building Release Version
```powershell
.\scripts\dev-env.ps1
dotnet build -c Release
```

### Running Tests
```powershell
.\scripts\dev-env.ps1
dotnet test
```

### Publishing Executable
```powershell
.\scripts\dev-env.ps1
dotnet publish src\ProjectSearcher.UI -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `src\ProjectSearcher.UI\bin\Release\net8.0\win-x64\publish\ProjectSearcher.exe`

## Tips

### Speed Up Search
- Keep scan interval reasonable (30-60 minutes)
- Use specific filters to narrow results
- Mark frequently used projects as favorites

### Organizing Projects
- Add locations to projects for filtering
- Use status to track active vs. completed
- Add tags for categorization
- Mark favorites for quick access

### Keyboard Efficiency
- Learn the filter syntax (`loc:`, `status:`, `year:`)
- Use short project numbers for faster typing
- Combine filters with semicolons

## Next Steps

1. **Add Metadata**: Right-click projects to add location, status, tags
2. **Mark Favorites**: Star frequently accessed projects
3. **Customize Hotkey**: Change in settings if needed
4. **Enable Auto-Start**: Launch with Windows automatically

## Support

For issues or questions:
1. Check `Crash.log` in application folder
2. Review `docs/BUILD_AND_RUN.md` for detailed troubleshooting
3. Check `docs/ARCHITECTURE.md` for technical details

---

**Version**: 1.0.0  
**Last Updated**: 2026-01-14
