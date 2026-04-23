<#
.SYNOPSIS
    Reset DesktopHub telemetry metrics data.

.DESCRIPTION
    Deletes telemetry events from the local metrics.db SQLite database.
    Supports filtering by user, device, event category, date range,
    or resetting everything. DesktopHub should be closed before running.

.PARAMETER All
    Reset ALL metrics data (deletes every row).

.PARAMETER User
    Delete events for a specific user name (matches session_id owner via data_json,
    or the machine's Environment.UserName recorded at event time).

.PARAMETER Device
    Delete events originating from a specific device/machine name.

.PARAMETER Category
    Delete events in a specific category (e.g. search, session, project_launch,
    widget, quick_launch, quick_task, doc_access, timer, cheat_sheet, hotkey,
    settings, filter, clipboard, error, performance).

.PARAMETER EventType
    Delete events of a specific event type (e.g. search_executed,
    smart_search_executed, session_start, etc.).

.PARAMETER Before
    Delete events older than this date (yyyy-MM-dd format).

.PARAMETER After
    Delete events newer than this date (yyyy-MM-dd format).

.PARAMETER QuerySource
    Delete events with a specific query_source (typed, pasted, history,
    frequent_project, smart_search, path_search, doc_search, etc.).

.PARAMETER DryRun
    Show what would be deleted without actually deleting.

.PARAMETER DbPath
    Override the default database path (%APPDATA%\DesktopHub\metrics.db).

.EXAMPLE
    # Reset all metrics
    .\Reset-Metrics.ps1 -All

.EXAMPLE
    # Delete all events for user "jsmith"
    .\Reset-Metrics.ps1 -User "jsmith"

.EXAMPLE
    # Delete all search events before 2025-01-01
    .\Reset-Metrics.ps1 -Category "search" -Before "2025-01-01"

.EXAMPLE
    # Delete only 'pasted' query source events (dry run)
    .\Reset-Metrics.ps1 -QuerySource "pasted" -DryRun

.EXAMPLE
    # Delete all events for device WORKSTATION-5
    .\Reset-Metrics.ps1 -Device "WORKSTATION-5"
#>

[CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = "Filtered")]
param(
    [Parameter(ParameterSetName = "ResetAll")]
    [switch]$All,

    [Parameter(ParameterSetName = "Filtered")]
    [string]$User,

    [Parameter(ParameterSetName = "Filtered")]
    [string]$Device,

    [Parameter(ParameterSetName = "Filtered")]
    [string]$Category,

    [Parameter(ParameterSetName = "Filtered")]
    [string]$EventType,

    [Parameter(ParameterSetName = "Filtered")]
    [string]$Before,

    [Parameter(ParameterSetName = "Filtered")]
    [string]$After,

    [Parameter(ParameterSetName = "Filtered")]
    [string]$QuerySource,

    [switch]$DryRun,

    [string]$DbPath
)

# ── Resolve DB path ──────────────────────────────────────────────────────────
if (-not $DbPath) {
    $DbPath = Join-Path $env:APPDATA "DesktopHub\metrics.db"
}

if (-not (Test-Path $DbPath)) {
    Write-Error "Database not found at: $DbPath"
    exit 1
}

Write-Host "Database: $DbPath" -ForegroundColor Cyan

# ── Load SQLite assembly ─────────────────────────────────────────────────────
# Try to find Microsoft.Data.Sqlite from the build output or NuGet cache
$sqliteDll = $null
$searchPaths = @(
    (Join-Path $PSScriptRoot "..\src\DesktopHub.UI\bin\Debug\net8.0-windows\Microsoft.Data.Sqlite.dll"),
    (Join-Path $PSScriptRoot "..\src\DesktopHub.UI\bin\Release\net8.0-windows\win-x64\Microsoft.Data.Sqlite.dll")
)
foreach ($p in $searchPaths) {
    if (Test-Path $p) { $sqliteDll = (Resolve-Path $p).Path; break }
}

$useSqliteManaged = $false
if ($sqliteDll) {
    try {
        Add-Type -Path $sqliteDll
        $useSqliteManaged = $true
        Write-Host "Loaded Microsoft.Data.Sqlite from build output" -ForegroundColor DarkGray
    } catch {
        Write-Host "Could not load Microsoft.Data.Sqlite, falling back to System.Data.SQLite" -ForegroundColor Yellow
    }
}

# Fallback: use raw ADO via sqlite3.exe or System.Data.SQLite
if (-not $useSqliteManaged) {
    # Check for sqlite3 CLI
    $sqlite3 = Get-Command sqlite3 -ErrorAction SilentlyContinue
    if (-not $sqlite3) {
        $sqlite3 = Get-Command sqlite3.exe -ErrorAction SilentlyContinue
    }
}

# ── Build WHERE clause ───────────────────────────────────────────────────────
$conditions = @()
$params = @{}

if ($All) {
    # No conditions = delete all
    Write-Host "Mode: DELETE ALL EVENTS" -ForegroundColor Red
}
else {
    if ($User) {
        # User name is stored in data_json for session events; we also check
        # if the widget_name or data_json contains the user
        $conditions += "(data_json LIKE @user_pattern OR widget_name LIKE @user_pattern)"
        $params["@user_pattern"] = "%$User%"
        Write-Host "Filter: User contains '$User'" -ForegroundColor Yellow
    }
    if ($Device) {
        $conditions += "data_json LIKE @device_pattern"
        $params["@device_pattern"] = "%$Device%"
        Write-Host "Filter: Device contains '$Device'" -ForegroundColor Yellow
    }
    if ($Category) {
        $conditions += "category = @category"
        $params["@category"] = $Category
        Write-Host "Filter: Category = '$Category'" -ForegroundColor Yellow
    }
    if ($EventType) {
        $conditions += "event_type = @event_type"
        $params["@event_type"] = $EventType
        Write-Host "Filter: EventType = '$EventType'" -ForegroundColor Yellow
    }
    if ($Before) {
        $conditions += "timestamp < @before"
        $params["@before"] = ([DateTime]::Parse($Before)).ToString("O")
        Write-Host "Filter: Before $Before" -ForegroundColor Yellow
    }
    if ($After) {
        $conditions += "timestamp >= @after"
        $params["@after"] = ([DateTime]::Parse($After)).ToString("O")
        Write-Host "Filter: After $After" -ForegroundColor Yellow
    }
    if ($QuerySource) {
        $conditions += "query_source = @query_source"
        $params["@query_source"] = $QuerySource
        Write-Host "Filter: QuerySource = '$QuerySource'" -ForegroundColor Yellow
    }

    if ($conditions.Count -eq 0) {
        Write-Error "No filters specified. Use -All to reset everything, or specify at least one filter."
        Write-Host ""
        Write-Host "Available filters:" -ForegroundColor Cyan
        Write-Host "  -User <name>        Filter by user name"
        Write-Host "  -Device <name>      Filter by device/machine name"
        Write-Host "  -Category <cat>     Filter by event category (search, session, widget, etc.)"
        Write-Host "  -EventType <type>   Filter by event type (search_executed, etc.)"
        Write-Host "  -Before <date>      Events before date (yyyy-MM-dd)"
        Write-Host "  -After <date>       Events after date (yyyy-MM-dd)"
        Write-Host "  -QuerySource <src>  Filter by query source (typed, pasted, frequent_project, etc.)"
        Write-Host ""
        Write-Host "Available categories: search, session, project_launch, widget, quick_launch,"
        Write-Host "  quick_task, doc_access, timer, cheat_sheet, hotkey, settings, filter,"
        Write-Host "  clipboard, error, performance"
        Write-Host ""
        Write-Host "Available query sources: typed, pasted, frequent_project, history,"
        Write-Host "  smart_search, path_search, doc_search, hotkey_direct, widget_launcher"
        exit 1
    }
}

$whereClause = if ($conditions.Count -gt 0) { "WHERE " + ($conditions -join " AND ") } else { "" }
$countSql = "SELECT COUNT(*) FROM telemetry_events $whereClause"
$deleteSql = "DELETE FROM telemetry_events $whereClause"

# ── Execute ──────────────────────────────────────────────────────────────────
function Invoke-SqliteQuery {
    param([string]$Sql, [hashtable]$Parameters = @{}, [bool]$Scalar = $false)

    if ($useSqliteManaged) {
        $conn = New-Object Microsoft.Data.Sqlite.SqliteConnection "Data Source=$DbPath"
        $conn.Open()
        try {
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = $Sql
            foreach ($kv in $Parameters.GetEnumerator()) {
                $cmd.Parameters.AddWithValue($kv.Key, $kv.Value) | Out-Null
            }
            if ($Scalar) {
                return $cmd.ExecuteScalar()
            } else {
                return $cmd.ExecuteNonQuery()
            }
        } finally {
            $conn.Close()
            $conn.Dispose()
        }
    }
    elseif ($sqlite3) {
        # Fallback to sqlite3 CLI (no parameterized queries, so escape values)
        $safeSql = $Sql
        foreach ($kv in $Parameters.GetEnumerator()) {
            $escaped = $kv.Value -replace "'", "''"
            $safeSql = $safeSql -replace [regex]::Escape($kv.Key), "'$escaped'"
        }
        $result = & $sqlite3.Source $DbPath $safeSql 2>&1
        return $result
    }
    else {
        Write-Error "No SQLite driver available. Build the project first or install sqlite3."
        exit 1
    }
}

# Count matching rows
$count = Invoke-SqliteQuery -Sql $countSql -Parameters $params -Scalar $true
Write-Host ""
Write-Host "Matching events: $count" -ForegroundColor White

if ([int]$count -eq 0) {
    Write-Host "Nothing to delete." -ForegroundColor Green
    exit 0
}

if ($DryRun) {
    Write-Host "[DRY RUN] Would delete $count event(s). No changes made." -ForegroundColor Magenta

    # Show a sample of what would be deleted
    $sampleSql = "SELECT category, event_type, timestamp, query_text, query_source FROM telemetry_events $whereClause LIMIT 10"
    if ($useSqliteManaged) {
        $conn = New-Object Microsoft.Data.Sqlite.SqliteConnection "Data Source=$DbPath"
        $conn.Open()
        try {
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = $sampleSql
            foreach ($kv in $params.GetEnumerator()) {
                $cmd.Parameters.AddWithValue($kv.Key, $kv.Value) | Out-Null
            }
            $reader = $cmd.ExecuteReader()
            Write-Host ""
            Write-Host "Sample of events that would be deleted:" -ForegroundColor Cyan
            Write-Host ("{0,-20} {1,-30} {2,-22} {3,-20} {4}" -f "Category", "EventType", "Timestamp", "Query", "Source")
            Write-Host ("{0,-20} {1,-30} {2,-22} {3,-20} {4}" -f "--------", "---------", "---------", "-----", "------")
            while ($reader.Read()) {
                $cat = if ($reader.IsDBNull(0)) { "-" } else { $reader.GetString(0) }
                $evt = if ($reader.IsDBNull(1)) { "-" } else { $reader.GetString(1) }
                $ts  = if ($reader.IsDBNull(2)) { "-" } else { $reader.GetString(2).Substring(0, [Math]::Min(19, $reader.GetString(2).Length)) }
                $qt  = if ($reader.IsDBNull(3)) { "-" } else { $reader.GetString(3) }
                $qs  = if ($reader.IsDBNull(4)) { "-" } else { $reader.GetString(4) }
                if ($qt.Length -gt 18) { $qt = $qt.Substring(0, 18) + ".." }
                Write-Host ("{0,-20} {1,-30} {2,-22} {3,-20} {4}" -f $cat, $evt, $ts, $qt, $qs)
            }
        } finally {
            $conn.Close()
            $conn.Dispose()
        }
    }
    exit 0
}

# Confirm
Write-Host ""
$confirm = Read-Host "Delete $count event(s)? (y/N)"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "Aborted." -ForegroundColor Yellow
    exit 0
}

# Delete
$deleted = Invoke-SqliteQuery -Sql $deleteSql -Parameters $params
Write-Host "Deleted $count event(s) from metrics.db" -ForegroundColor Green

# Vacuum to reclaim space
try {
    Invoke-SqliteQuery -Sql "VACUUM" | Out-Null
    Write-Host "Database vacuumed." -ForegroundColor DarkGray
} catch {
    Write-Host "Vacuum skipped: $_" -ForegroundColor DarkGray
}

Write-Host "Done." -ForegroundColor Green
