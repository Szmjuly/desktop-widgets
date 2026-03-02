# DesktopHub Metrics Registry

> **Single source of truth** for all telemetry events collected by DesktopHub.
> Updated: 2026-02-27

## Architecture

| Layer | Store | Purpose |
|-------|-------|---------|
| Raw events | Local SQLite (`%LOCALAPPDATA%\DesktopHub\metrics.db`) | Per-device, fast writes, offline resilient, 90-day retention |
| Aggregated summaries | Firebase RTDB (`metrics/{deviceId}/{date}`) | Cross-user analytics, synced every 30 min |
| Feature flags | Firebase RTDB (`feature_flags/`) | 3-tier: device → license → global |

## Categories & Event Types

### Session (`session`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `session_start` | App launched, new session begun | `sessionId` | `TelemetryService.InitializeAsync` |
| `session_end` | App closing, session duration recorded | `durationMs` | `TelemetryService.EndSessionAsync` |

### Search (`search`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `search_executed` | User typed a project search query | `queryText`, `resultCount` | `SearchOverlay.xaml.cs` |
| `search_result_clicked` | User clicked a search result | `queryText`, `resultIndex` | `SearchOverlay.ResultsInteraction.cs` |
| `search_project_launched` | User opened a project from search | `projectNumber`, `projectType` | `SearchOverlay.ResultsInteraction.cs` |
| `path_search_executed` | Path-scoped search triggered | `queryText`, `resultCount` | `SearchOverlay.PathSearch.cs` |
| `path_result_clicked` | Path search result opened | `queryText`, `resultIndex` | `SearchOverlay.PathSearch.cs` |
| `smart_search_executed` | Smart Project Search query | `queryText`, `resultCount`, `widgetName` | `SmartProjectSearchWidget.xaml.cs` |
| `smart_search_result_clicked` | Smart search result file opened | `queryText`, `resultIndex` | `SmartProjectSearchWidget.xaml.cs` |

### Doc Access (`doc_access`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `doc_search_executed` | Doc Quick Open search query | `discipline`, `queryText`, `resultCount`, `projectType` | `DocQuickOpenWidget.xaml.cs` |
| `doc_opened` | Document file opened | `discipline`, `fileExtension`, `projectType` | `DocQuickOpenWidget.xaml.cs` |

### Project Launch (`project_launch`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `search_project_launched` | Project launched from any source | `projectNumber`, `projectType`, `widgetName` (source) | `SearchOverlay`, `FrequentProjectsWidget` |
| `frequent_project_opened` | Frequent project double-clicked | `projectNumber` | `FrequentProjectsWidget.xaml.cs` |

### Quick Launch (`quick_launch`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `quick_launch_item_launched` | Quick Launch item opened | `itemType` | `QuickLaunchWidget.xaml.cs` |
| `quick_launch_item_added` | Item added (manual or drag-drop) | `itemType` | `QuickLaunchWidget.xaml.cs` |
| `quick_launch_item_removed` | Item removed | — | `QuickLaunchWidget.xaml.cs` |

### Quick Tasks (`quick_task`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `task_created` | New task added | `charCount`, `taskCountAtTime` | `QuickTasksWidget.xaml.cs` |
| `task_completed` | Task completion toggled | `durationMs` (time to complete) | `QuickTasksWidget.xaml.cs` |
| `task_deleted` | Task deleted | — | `QuickTasksWidget.xaml.cs` |

### Timer (`timer`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `timer_started` | Timer started | — | `TimerWidget.xaml.cs` |
| `timer_stopped` | Timer stopped | `durationSeconds` | `TimerWidget.xaml.cs` |

### Cheat Sheet (`cheat_sheet`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `cheat_sheet_viewed` | Cheat sheet opened/viewed | `sheetId`, `timeVisibleMs` | `CheatSheetWidget.xaml.cs` |

### Widget Visibility (`widget`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `widget_opened` | Widget became visible | `widgetName` | `SearchOverlay.WidgetWindows.cs` |
| `widget_closed` | Widget became hidden | `widgetName` | `SearchOverlay.WidgetWindows.cs` |

**Tracked widgets:** Timer, QuickTasks, DocQuickOpen, FrequentProjects, QuickLaunch, SmartProjectSearch, CheatSheet, MetricsViewer

### Hotkey (`hotkey`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `hotkey_pressed` | Global hotkey activated | `hotkeyGroup`, `widgetCount` | `SearchOverlay.VisibilityAndHotkey.cs` ✅ |

### Settings (`settings`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `setting_changed` | User changed a setting | `settingName`, `newValue` | `SettingsWindow.xaml.cs` ✅ (auto_start, q/p_drive_enabled) |

### Filter (`filter`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `filter_changed` | Search filter changed | `filterType`, `filterValue` | `SearchOverlay.ResultsInteraction.cs` ✅ (year, drive_location) |
| `discipline_changed` | Doc discipline selection | `discipline`, `projectType` | `DocQuickOpenWidget.xaml.cs` ✅ (via TrackDocAccess) |

### Clipboard (`clipboard`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `clipboard_copy` | Path/text copied to clipboard | `copyType`, `widgetName` | `SearchOverlay.ResultsInteraction.cs` ✅, `DocQuickOpenWidget.xaml.cs` ✅, `CheatSheetWidget.xaml.cs` ✅ |

### Error (`error`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `app_error` | Unhandled exception caught | `errorType`, `context`, `message` | `App.xaml.cs` ✅ (DispatcherUnhandled + AppDomain) |
| `widget_error` | Widget-level error | `widgetName`, `errorType`, `message` | *Future: instrument per-widget catch blocks* |

### Performance (`performance`)
| Event Type | Description | Key Fields | Instrumented In |
|-----------|-------------|------------|-----------------|
| `startup_timing` | App startup completed | `phase`, `durationMs` | `App.xaml.cs` ✅ |
| `search_timing` | Search operation completed | `searchType`, `durationMs`, `resultCount` | *Future: instrument search timing* |

## Aggregated Daily Summary (Firebase)

The `DailyMetricsSummary` model aggregates the following for each device per day:

- `SessionCount`, `TotalSessionDurationMs`
- `TotalSearches`, `TotalSmartSearches`, `TotalDocSearches`, `TotalPathSearches`
- `TotalProjectLaunches`, `TotalQuickLaunchUses`, `TotalQuickLaunchAdds`, `TotalQuickLaunchRemoves`
- `TotalTasksCreated`, `TotalTasksCompleted`
- `TotalDocOpens`, `TotalTimerUses`, `TotalCheatSheetViews`
- `WidgetUsageCounts` — map of widget ID → open count
- `ProjectTypeFrequency` — map of project type → count
- `DisciplineFrequency` — map of discipline → count
- `TopSearchQueries` — top 10 search queries by frequency

## Data Privacy

- **No sensitive data logged**: No file contents, user credentials, or PII
- **Query text**: Stored for search analytics only (project numbers, search terms)
- **Local retention**: Raw events purged after 90 days
- **Firebase**: Aggregated summaries only (no raw queries synced)
