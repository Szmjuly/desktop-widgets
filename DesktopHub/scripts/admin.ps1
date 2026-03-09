#!/usr/bin/env pwsh
<#
.SYNOPSIS
    DesktopHub Admin Console - Master administration script.

.DESCRIPTION
    Interactive menu for all DesktopHub administrative operations:
    database management, tag management, builds, version updates,
    user/device management, and metrics.

    Run without parameters for interactive menu, or use -Action for direct execution.

.PARAMETER Action
    Run a specific action directly (non-interactive):
    db-dump, db-wipe-all, db-wipe-devices, db-wipe-tags,
    tags-get, tags-list, tags-decrypt, tags-set, tags-delete, tags-export, tags-import,
    admin-list, admin-add, admin-remove,
    auth-cleanup, auth-cleanup-anon,
    metrics-reset,
    version-update,
    build, build-installer,
    show-secret

.PARAMETER ProjectNumber
    Project number for tag operations (e.g., "2024278.01")

.PARAMETER TagKey
    Tag field key for tag set/delete (e.g., "voltage", "hvac_type")

.PARAMETER TagValue
    Tag value for tag set operation

.PARAMETER Username
    Username for admin add/remove operations

.PARAMETER Version
    Version string for version-update (e.g., "1.2.0")

.PARAMETER ReleaseNotes
    Release notes for version-update

.PARAMETER CsvFile
    CSV file path for tags-export/tags-import

.PARAMETER ServiceAccountPath
    Path to Firebase service account JSON (auto-detected if omitted)

.EXAMPLE
    .\admin.ps1                                          # Interactive menu
    .\admin.ps1 -Action db-dump                          # Dump database structure
    .\admin.ps1 -Action tags-get -ProjectNumber "2024278.01"
    .\admin.ps1 -Action admin-add -Username "jdoe"
    .\admin.ps1 -Action version-update -Version "1.2.0" -ReleaseNotes "Bug fixes"
#>

param(
    [string]$Action,
    [string]$ProjectNumber,
    [string]$TagKey,
    [string]$TagValue,
    [string]$Username,
    [string]$Version,
    [string]$ReleaseNotes,
    [string]$CsvFile,
    [string]$ServiceAccountPath
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
}
if ([string]::IsNullOrWhiteSpace($scriptDir)) {
    $scriptDir = $PWD.Path
}
if (-not [System.IO.Path]::IsPathRooted($scriptDir)) {
    $scriptDir = Join-Path $PWD.Path $scriptDir
}
try {
    $scriptDir = (Resolve-Path $scriptDir).Path
} catch {}

$script:resolvedSaPath = $null
$saCandidates = @()
if (-not [string]::IsNullOrWhiteSpace($ServiceAccountPath)) {
    $saCandidates += $ServiceAccountPath
}
$saCandidates += @(
    (Join-Path $scriptDir "..\secrets\firebase-license.json"),
    (Join-Path $scriptDir "..\..\Renamer\firebase-admin-key.json"),
    (Join-Path $scriptDir "..\..\firebase-admin-key.json"),
    (Join-Path $scriptDir "firebase-license.json"),
    (Join-Path (Split-Path -Parent $scriptDir) "secrets\firebase-license.json"),
    (Join-Path (Split-Path -Parent $scriptDir) "..\Renamer\firebase-admin-key.json"),
    (Join-Path $PWD.Path "DesktopHub\secrets\firebase-license.json"),
    (Join-Path $PWD.Path "Renamer\firebase-admin-key.json"),
    (Join-Path $PWD.Path "firebase-license.json")
)
$saCandidates = @($saCandidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
foreach ($c in $saCandidates) {
    try {
        if (Test-Path $c) {
            $script:resolvedSaPath = (Resolve-Path $c).Path
            break
        }
    } catch {}
}
if ($script:resolvedSaPath) {
    $ServiceAccountPath = $script:resolvedSaPath
}

# ============================================================
# HELPERS
# ============================================================

function Show-Banner {
    Write-Host ""
    Write-Host "  ============================================" -ForegroundColor Cyan
    Write-Host "       DesktopHub Admin Console" -ForegroundColor White
    Write-Host "  ============================================" -ForegroundColor Cyan
    Write-Host ""
}

function Show-Menu {
    Write-Host "  DATABASE" -ForegroundColor Yellow
    Write-Host "    [1]  Dump database structure"
    Write-Host "    [2]  Dump database (full detail)"
    Write-Host "    [3]  Wipe devices node"
    Write-Host "    [4]  Wipe project tags"
    Write-Host "    [5]  Wipe ALL (full reset, preserves licenses/versions/admins)"
    Write-Host ""
    Write-Host "  PROJECT TAGS" -ForegroundColor Yellow
    Write-Host "    [10] Get tags for a project"
    Write-Host "    [11] List all tag entries (summary)"
    Write-Host "    [12] Decrypt & dump all tags (readable)"
    Write-Host "    [13] Set a tag value"
    Write-Host "    [14] Delete a tag / all tags for a project"
    Write-Host "    [15] Export tags to CSV"
    Write-Host "    [16] Import tags from CSV"
    Write-Host "    [17] Show HMAC secret (for sharing)"
    Write-Host ""
    Write-Host "  ADMIN USERS" -ForegroundColor Yellow
    Write-Host "    [20] List admin users"
    Write-Host "    [21] Add admin user"
    Write-Host "    [22] Remove admin user"
    Write-Host ""
    Write-Host "  AUTH / USERS" -ForegroundColor Yellow
    Write-Host "    [30] Preview Firebase Auth users"
    Write-Host "    [31] Delete all Firebase Auth users"
    Write-Host "    [32] Delete anonymous Auth users only"
    Write-Host ""
    Write-Host "  METRICS" -ForegroundColor Yellow
    Write-Host "    [40] Reset local metrics (interactive)"
    Write-Host "    [41] Reset ALL local metrics"
    Write-Host ""
    Write-Host "  BUILD & RELEASE" -ForegroundColor Yellow
    Write-Host "    [50] Build single-file executable"
    Write-Host "    [51] Build installer"
    Write-Host "    [52] Update Firebase version"
    Write-Host ""
    Write-Host "    [H]  Help" -ForegroundColor DarkGray
    Write-Host "    [Q]  Quit" -ForegroundColor DarkGray
    Write-Host ""
}

function Invoke-Script {
    param([string]$Script, [string[]]$ScriptArgs)
    $path = Join-Path $scriptDir $Script
    if (-not (Test-Path $path)) {
        Write-Host "  Script not found: $path" -ForegroundColor Red
        return
    }
    Write-Host ""
    if ($script:resolvedSaPath -and ($ScriptArgs -notcontains "-ServiceAccountPath")) {
        & $path @ScriptArgs -ServiceAccountPath $script:resolvedSaPath
    } else {
        & $path @ScriptArgs
    }
    Write-Host ""
    Write-Host "  Press Enter to continue..." -ForegroundColor DarkGray
    Read-Host | Out-Null
}

function Prompt-Input {
    param([string]$Label, [string]$Default)
    if ($Default) {
        $input = Read-Host "  $Label [$Default]"
        if ([string]::IsNullOrWhiteSpace($input)) { return $Default }
        return $input
    }
    return Read-Host "  $Label"
}

function Build-SaArg {
    if ($script:resolvedSaPath) { return @("-ServiceAccountPath", $script:resolvedSaPath) }
    return @()
}

# ============================================================
# DIRECT ACTION MODE (non-interactive)
# ============================================================

if ($Action) {
    $sa = Build-SaArg

    switch ($Action) {
        "db-dump"          { & "$scriptDir\dump-database.ps1" @sa }
        "db-wipe-all"      { & "$scriptDir\dump-database.ps1" -WipeAll @sa }
        "db-wipe-devices"  { & "$scriptDir\dump-database.ps1" -WipeDevices @sa }
        "db-wipe-tags"     { & "$scriptDir\dump-database.ps1" -WipeTags @sa }
        "tags-get"         { & "$scriptDir\tag-manager.ps1" -Action get -ProjectNumber $ProjectNumber @sa }
        "tags-list"        { & "$scriptDir\tag-manager.ps1" -Action list @sa }
        "tags-decrypt"     { & "$scriptDir\tag-manager.ps1" -Action decrypt-dump @sa }
        "tags-set"         { & "$scriptDir\tag-manager.ps1" -Action set -ProjectNumber $ProjectNumber -TagKey $TagKey -TagValue $TagValue @sa }
        "tags-delete"      { & "$scriptDir\tag-manager.ps1" -Action delete -ProjectNumber $ProjectNumber -TagKey $TagKey @sa }
        "tags-export"      { & "$scriptDir\tag-manager.ps1" -Action export -CsvFile $CsvFile @sa }
        "tags-import"      { & "$scriptDir\tag-manager.ps1" -Action import -CsvFile $CsvFile @sa }
        "admin-list"       { & "$scriptDir\manage-admin.ps1" -Action list @sa }
        "admin-add"        { & "$scriptDir\manage-admin.ps1" -Action add -Username $Username @sa }
        "admin-remove"     { & "$scriptDir\manage-admin.ps1" -Action remove -Username $Username @sa }
        "auth-cleanup"     { & "$scriptDir\cleanup-auth-users.ps1" -DeleteAll @sa }
        "auth-cleanup-anon"{ & "$scriptDir\cleanup-auth-users.ps1" -DeleteAll -AnonymousOnly @sa }
        "metrics-reset"    { & "$scriptDir\Reset-Metrics.ps1" -All }
        "version-update"   {
            $notes = if ($ReleaseNotes) { $ReleaseNotes } else { "New version available" }
            & "$scriptDir\Update-FirebaseVersion.ps1" -Version $Version -ReleaseNotes $notes @sa
        }
        "build"            { & "$scriptDir\build-single-file.ps1" }
        "build-installer"  { & "$scriptDir\build-installer.ps1" }
        "show-secret"      { & "$scriptDir\tag-manager.ps1" -Action show-secret }
        default            { Write-Host "Unknown action: $Action" -ForegroundColor Red; Write-Host "Run: .\admin.ps1 -Action help" }
    }
    exit 0
}

# ============================================================
# INTERACTIVE MENU LOOP
# ============================================================

Show-Banner

while ($true) {
    Show-Menu
    $choice = Read-Host "  Select option"

    $sa = Build-SaArg

    switch ($choice.ToUpper()) {
        # --- DATABASE ---
        "1" { Invoke-Script "dump-database.ps1" @sa }
        "2" { Invoke-Script "dump-database.ps1" (@("-Full") + $sa) }
        "3" { Invoke-Script "dump-database.ps1" (@("-WipeDevices", "-Force") + $sa) }
        "4" { Invoke-Script "dump-database.ps1" (@("-WipeTags", "-Force") + $sa) }
        "5" {
            Write-Host ""
            Write-Host "  WARNING: This will wipe EVERYTHING except app_versions and admin_users." -ForegroundColor Red
            $confirm = Read-Host "  Type 'WIPE' to confirm full reset"
            if ($confirm -eq "WIPE") {
                Invoke-Script "dump-database.ps1" (@("-WipeAll", "-Force") + $sa)
            } else {
                Write-Host "  Cancelled." -ForegroundColor Gray
            }
        }

        # --- PROJECT TAGS ---
        "10" {
            $pn = if ($ProjectNumber) { $ProjectNumber } else { Prompt-Input "Project number" }
            Invoke-Script "tag-manager.ps1" (@("-Action", "get", "-ProjectNumber", $pn) + $sa)
        }
        "11" { Invoke-Script "tag-manager.ps1" (@("-Action", "list") + $sa) }
        "12" { Invoke-Script "tag-manager.ps1" (@("-Action", "decrypt-dump") + $sa) }
        "13" {
            $pn = if ($ProjectNumber) { $ProjectNumber } else { Prompt-Input "Project number" }
            $tk = if ($TagKey) { $TagKey } else { Prompt-Input "Tag key (e.g. voltage, hvac_type)" }
            $tv = if ($TagValue) { $TagValue } else { Prompt-Input "Tag value" }
            Invoke-Script "tag-manager.ps1" (@("-Action", "set", "-ProjectNumber", $pn, "-TagKey", $tk, "-TagValue", $tv) + $sa)
        }
        "14" {
            $pn = if ($ProjectNumber) { $ProjectNumber } else { Prompt-Input "Project number" }
            $tk = Prompt-Input "Tag key (leave blank to delete ALL tags)"
            $args = @("-Action", "delete", "-ProjectNumber", $pn)
            if ($tk) { $args += @("-TagKey", $tk) }
            Invoke-Script "tag-manager.ps1" ($args + $sa)
        }
        "15" {
            $csv = if ($CsvFile) { $CsvFile } else { Prompt-Input "Output CSV file path" "tags_export.csv" }
            Invoke-Script "tag-manager.ps1" (@("-Action", "export", "-CsvFile", $csv) + $sa)
        }
        "16" {
            $csv = if ($CsvFile) { $CsvFile } else { Prompt-Input "Input CSV file path" }
            Invoke-Script "tag-manager.ps1" (@("-Action", "import", "-CsvFile", $csv) + $sa)
        }
        "17" { Invoke-Script "tag-manager.ps1" @("-Action", "show-secret") }

        # --- ADMIN USERS ---
        "20" { Invoke-Script "manage-admin.ps1" (@("-Action", "list") + $sa) }
        "21" {
            $un = if ($Username) { $Username } else { Prompt-Input "Username to add" }
            Invoke-Script "manage-admin.ps1" (@("-Action", "add", "-Username", $un) + $sa)
        }
        "22" {
            $un = if ($Username) { $Username } else { Prompt-Input "Username to remove" }
            Invoke-Script "manage-admin.ps1" (@("-Action", "remove", "-Username", $un) + $sa)
        }

        # --- AUTH / USERS ---
        "30" { Invoke-Script "cleanup-auth-users.ps1" $sa }
        "31" { Invoke-Script "cleanup-auth-users.ps1" (@("-DeleteAll") + $sa) }
        "32" { Invoke-Script "cleanup-auth-users.ps1" (@("-DeleteAll", "-AnonymousOnly") + $sa) }

        # --- METRICS ---
        "40" { Invoke-Script "Reset-Metrics.ps1" @() }
        "41" { Invoke-Script "Reset-Metrics.ps1" @("-All") }

        # --- BUILD & RELEASE ---
        "50" { Invoke-Script "build-single-file.ps1" @() }
        "51" { Invoke-Script "build-installer.ps1" @() }
        "52" {
            $ver = if ($Version) { $Version } else { Prompt-Input "Version (e.g. 1.2.0)" }
            $notes = if ($ReleaseNotes) { $ReleaseNotes } else { Prompt-Input "Release notes" "New version available" }
            Invoke-Script "Update-FirebaseVersion.ps1" (@("-Version", $ver, "-ReleaseNotes", $notes) + $sa)
        }

        # --- HELP ---
        "H" {
            Write-Host ""
            Write-Host "  DesktopHub Admin Console Help" -ForegroundColor Cyan
            Write-Host "  =============================" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "  Interactive mode:" -ForegroundColor Yellow
            Write-Host "    .\admin.ps1                              # Launch menu"
            Write-Host ""
            Write-Host "  Direct actions (non-interactive):" -ForegroundColor Yellow
            Write-Host "    .\admin.ps1 -Action db-dump"
            Write-Host "    .\admin.ps1 -Action db-wipe-tags"
            Write-Host "    .\admin.ps1 -Action db-wipe-all"
            Write-Host "    .\admin.ps1 -Action tags-get -ProjectNumber '2024278.01'"
            Write-Host "    .\admin.ps1 -Action tags-decrypt"
            Write-Host "    .\admin.ps1 -Action tags-set -ProjectNumber '2024278.01' -TagKey voltage -TagValue 208"
            Write-Host "    .\admin.ps1 -Action admin-list"
            Write-Host "    .\admin.ps1 -Action admin-add -Username jdoe"
            Write-Host "    .\admin.ps1 -Action auth-cleanup-anon"
            Write-Host "    .\admin.ps1 -Action metrics-reset"
            Write-Host "    .\admin.ps1 -Action version-update -Version 1.2.0 -ReleaseNotes 'Bug fixes'"
            Write-Host "    .\admin.ps1 -Action build"
            Write-Host "    .\admin.ps1 -Action show-secret"
            Write-Host ""
            Write-Host "  Service account:" -ForegroundColor Yellow
            Write-Host "    Auto-detected from secrets/firebase-license.json"
            Write-Host "    Override: -ServiceAccountPath 'path\to\sa.json'"
            Write-Host ""
            Write-Host "  Scripts folder structure:" -ForegroundColor Yellow
            Write-Host "    scripts/admin.ps1               # This master script"
            Write-Host "    scripts/dump-database.ps1        # Firebase DB viewer & wiper"
            Write-Host "    scripts/tag-manager.ps1          # Project tag CRUD + encryption"
            Write-Host "    scripts/manage-admin.ps1         # Admin user management"
            Write-Host "    scripts/cleanup-auth-users.ps1   # Firebase Auth cleanup"
            Write-Host "    scripts/wipe-devices.ps1         # Device node wiper"
            Write-Host "    scripts/Reset-Metrics.ps1        # Local metrics reset"
            Write-Host "    scripts/Update-FirebaseVersion.ps1  # Version updater"
            Write-Host "    scripts/build-single-file.ps1    # Single-file build"
            Write-Host "    scripts/build-installer.ps1      # Installer build"
            Write-Host "    scripts/_Archive/                 # Archived/obsolete scripts"
            Write-Host ""
            Write-Host "  Press Enter to return to menu..." -ForegroundColor DarkGray
            Read-Host | Out-Null
        }

        # --- QUIT ---
        "Q" {
            Write-Host "  Goodbye." -ForegroundColor DarkGray
            exit 0
        }

        default {
            Write-Host "  Invalid option: $choice" -ForegroundColor Red
            Start-Sleep -Milliseconds 500
        }
    }

    # Redraw banner
    Clear-Host
    Show-Banner
}
