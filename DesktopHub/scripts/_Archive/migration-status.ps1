#!/usr/bin/env pwsh
# Firebase Migration Status & Cleanup Tool
# Shows migration progress from legacy flat structure to new users/devices/events structure.
#
# Authentication: Uses service account JSON (same credentials as DesktopHub).
# The service account gets an OAuth2 access_token that bypasses all security rules (full admin).
#
# Usage:
#   .\migration-status.ps1                                          # Auto-finds service account
#   .\migration-status.ps1 -ServiceAccountPath "path\to\sa.json"   # Explicit path
#   .\migration-status.ps1 -Clean                                   # Wipe safe legacy nodes
#
# Safe to wipe (write-only, no app reads from these):
#   app_launches, processing_sessions, error_logs, update_checks, device_heartbeats
#
# Must keep:
#   app_versions, licenses, admin_users, device_activations (read by apps)

param(
    [switch]$Clean,
    [string]$ServiceAccountPath
)

$baseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com"

# ============================================================
# SERVICE ACCOUNT AUTH (same mechanism as DesktopHub C# app)
# ============================================================

function ConvertTo-Base64Url([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Get-ServiceAccountAccessToken([string]$jsonPath) {
    $sa = Get-Content $jsonPath -Raw | ConvertFrom-Json
    $email = $sa.client_email
    $privateKeyPem = $sa.private_key

    # Unix timestamp (PS 5.1 compatible)
    $epoch = [DateTime]::new(1970, 1, 1, 0, 0, 0, [DateTimeKind]::Utc)
    $now = [long][Math]::Floor(([DateTime]::UtcNow - $epoch).TotalSeconds)

    $header = '{"alg":"RS256","typ":"JWT"}'
    $claims = @{
        iss   = $email
        scope = "https://www.googleapis.com/auth/firebase.database https://www.googleapis.com/auth/userinfo.email"
        aud   = "https://oauth2.googleapis.com/token"
        iat   = $now
        exp   = $now + 3600
    } | ConvertTo-Json -Compress

    $headerB64 = ConvertTo-Base64Url([System.Text.Encoding]::UTF8.GetBytes($header))
    $claimsB64 = ConvertTo-Base64Url([System.Text.Encoding]::UTF8.GetBytes($claims))
    $unsigned = "$headerB64.$claimsB64"

    # Parse PEM private key to raw PKCS8 bytes
    $keyText = $privateKeyPem -replace '-----BEGIN PRIVATE KEY-----', '' -replace '-----END PRIVATE KEY-----', '' -replace '\s+', ''
    $keyBytes = [Convert]::FromBase64String($keyText)

    # Load RSA key — try .NET Core method first, fall back to CNG (PS 5.1/Windows)
    $rsa = $null
    try {
        $rsa = [System.Security.Cryptography.RSA]::Create()
        $bytesRead = 0
        $rsa.ImportPkcs8PrivateKey($keyBytes, [ref]$bytesRead)
    } catch {
        try {
            $cngKey = [System.Security.Cryptography.CngKey]::Import($keyBytes, [System.Security.Cryptography.CngKeyBlobFormat]::Pkcs8PrivateBlob)
            $rsa = [System.Security.Cryptography.RSACng]::new($cngKey)
        } catch {
            throw "Failed to load service account private key. Error: $($_.Exception.Message)"
        }
    }

    $sigBytes = $rsa.SignData(
        [System.Text.Encoding]::UTF8.GetBytes($unsigned),
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
    )
    $sigB64 = ConvertTo-Base64Url($sigBytes)
    $jwt = "$unsigned.$sigB64"

    # Exchange JWT for access token
    $response = Invoke-RestMethod -Uri "https://oauth2.googleapis.com/token" -Method Post -Body @{
        grant_type = "urn:ietf:params:oauth:grant-type:jwt-bearer"
        assertion  = $jwt
    } -ContentType "application/x-www-form-urlencoded" -TimeoutSec 15

    return $response.access_token
}

# Find service account JSON
if ([string]::IsNullOrWhiteSpace($ServiceAccountPath)) {
    $scriptDir = $PSScriptRoot
    $candidates = @(
        (Join-Path $scriptDir "..\secrets\firebase-license.json"),
        (Join-Path $scriptDir "..\..\Renamer\firebase-admin-key.json"),
        (Join-Path $scriptDir "..\..\firebase-admin-key.json")
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $ServiceAccountPath = (Resolve-Path $c).Path; break }
    }
}

if ([string]::IsNullOrWhiteSpace($ServiceAccountPath) -or -not (Test-Path $ServiceAccountPath)) {
    Write-Host "Error: Service account JSON not found." -ForegroundColor Red
    Write-Host "Looked in:" -ForegroundColor Yellow
    Write-Host "  DesktopHub\secrets\firebase-license.json" -ForegroundColor Yellow
    Write-Host "  Renamer\firebase-admin-key.json" -ForegroundColor Yellow
    Write-Host "Or provide: -ServiceAccountPath <path>" -ForegroundColor Yellow
    exit 1
}

Write-Host "Authenticating with service account..." -ForegroundColor Gray
try {
    $script:accessToken = Get-ServiceAccountAccessToken $ServiceAccountPath
    Write-Host "Authenticated OK." -ForegroundColor Green
} catch {
    Write-Host "Error: Failed to get access token: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

function Get-FirebaseUrl([string]$path) {
    return "$baseUrl/$path.json?access_token=$($script:accessToken)"
}

function Read-FirebaseNode([string]$path) {
    try {
        $url = Get-FirebaseUrl $path
        $result = Invoke-RestMethod -Uri $url -Method Get -ContentType "application/json" -TimeoutSec 10
        if ($null -eq $result -or $result -eq "null") { return $null }
        return $result
    } catch {
        return "ACCESS_DENIED"
    }
}

function Count-Children($node) {
    if ($null -eq $node -or $node -eq "ACCESS_DENIED") { return 0 }
    try {
        return @($node.PSObject.Properties).Count
    } catch {
        return 0
    }
}

function Delete-FirebaseNode([string]$path) {
    try {
        $url = Get-FirebaseUrl $path
        Invoke-RestMethod -Uri $url -Method Delete -ContentType "application/json" -TimeoutSec 10 | Out-Null
        return $true
    } catch {
        return $false
    }
}

# ============================================================
# READ ALL NODES
# ============================================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Firebase Migration Status Report" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Reading Firebase nodes..." -ForegroundColor Gray

$newUsers = Read-FirebaseNode "users"
$newDevices = Read-FirebaseNode "devices"
$newEvents = Read-FirebaseNode "events"
$newErrors = Read-FirebaseNode "errors"

$oldHeartbeats = Read-FirebaseNode "device_heartbeats"
$oldActivations = Read-FirebaseNode "device_activations"
$oldAppLaunches = Read-FirebaseNode "app_launches"
$oldProcessingSessions = Read-FirebaseNode "processing_sessions"
$oldErrorLogs = Read-FirebaseNode "error_logs"
$oldUpdateChecks = Read-FirebaseNode "update_checks"
$oldUsageLogs = Read-FirebaseNode "usage_logs"
$oldUserMetrics = Read-FirebaseNode "user_metrics"

$appVersions = Read-FirebaseNode "app_versions"
$adminUsers = Read-FirebaseNode "admin_users"
$licenses = Read-FirebaseNode "licenses"

# ============================================================
# NEW STRUCTURE STATUS
# ============================================================

Write-Host ""
Write-Host "--- New Structure (users/devices/events) ---" -ForegroundColor Green

$newUserCount = Count-Children $newUsers
$newDeviceCount = Count-Children $newDevices

Write-Host "  Users registered:    $newUserCount" -ForegroundColor White
if ($newUsers -and $newUsers -ne "ACCESS_DENIED") {
    foreach ($prop in $newUsers.PSObject.Properties) {
        $username = $prop.Name
        $userData = $prop.Value
        $deviceCount = 0
        try { $deviceCount = Count-Children $userData.devices } catch {}
        $lastSeen = try { $userData.last_seen } catch { "unknown" }
        Write-Host "    $username ($deviceCount device(s), last seen: $($lastSeen.Substring(0, [Math]::Min(19, $lastSeen.Length))))" -ForegroundColor Gray
    }
}

Write-Host "  Devices registered:  $newDeviceCount" -ForegroundColor White
if ($newDevices -and $newDevices -ne "ACCESS_DENIED") {
    foreach ($prop in $newDevices.PSObject.Properties) {
        $deviceId = $prop.Name
        $d = $prop.Value
        $shortId = $deviceId.Substring(0, [Math]::Min(12, $deviceId.Length))
        $dName = try { $d.device_name } catch { "?" }
        $dUser = try { $d.username } catch { "?" }
        $dStatus = try { $d.status } catch { "?" }
        $apps = @()
        try {
            if ($d.apps) {
                foreach ($appProp in $d.apps.PSObject.Properties) {
                    $ver = try { $appProp.Value.installed_version } catch { "?" }
                    $appStatus = try { $appProp.Value.status } catch { "?" }
                    $apps += "$($appProp.Name) v$ver [$appStatus]"
                }
            }
        } catch {}
        $appsStr = if ($apps.Count -gt 0) { $apps -join ", " } else { "(no apps)" }
        Write-Host "    $shortId... | $dName | user=$dUser | status=$dStatus" -ForegroundColor Gray
        Write-Host "      Apps: $appsStr" -ForegroundColor DarkGray
    }
}

$newEventApps = Count-Children $newEvents
$newErrorApps = Count-Children $newErrors
Write-Host "  Event streams:       $newEventApps app(s)" -ForegroundColor White
Write-Host "  Error streams:       $newErrorApps app(s)" -ForegroundColor White

# ============================================================
# OLD STRUCTURE STATUS
# ============================================================

Write-Host ""
Write-Host "--- Legacy Structure (old nodes) ---" -ForegroundColor Yellow

$oldHeartbeatCount = Count-Children $oldHeartbeats
$oldActivationCount = Count-Children $oldActivations
$oldLaunchCount = Count-Children $oldAppLaunches
$oldSessionCount = Count-Children $oldProcessingSessions
$oldErrorCount = Count-Children $oldErrorLogs
$oldCheckCount = Count-Children $oldUpdateChecks
$oldUsageLogCount = Count-Children $oldUsageLogs
$oldUserMetricCount = Count-Children $oldUserMetrics
$licenseCount = Count-Children $licenses

$accessDeniedNodes = @()

function Show-LegacyNode([string]$name, $node, [int]$count, [bool]$safeToWipe) {
    $statusIcon = if ($safeToWipe) { "[SAFE TO WIPE]" } else { "[KEEP]" }
    $statusColor = if ($safeToWipe) { "DarkYellow" } else { "Green" }
    
    if ($node -eq "ACCESS_DENIED") {
        Write-Host "  $name : (requires auth to read)" -ForegroundColor DarkGray
        $script:accessDeniedNodes += $name
    } else {
        Write-Host "  $name : $count entries  $statusIcon" -ForegroundColor $statusColor
    }
}

Show-LegacyNode "device_heartbeats    " $oldHeartbeats $oldHeartbeatCount $true
Show-LegacyNode "device_activations   " $oldActivations $oldActivationCount $true
Show-LegacyNode "app_launches         " $oldAppLaunches $oldLaunchCount $true
Show-LegacyNode "processing_sessions  " $oldProcessingSessions $oldSessionCount $true
Show-LegacyNode "error_logs           " $oldErrorLogs $oldErrorCount $true
Show-LegacyNode "update_checks        " $oldUpdateChecks $oldCheckCount $true
Show-LegacyNode "usage_logs           " $oldUsageLogs $oldUsageLogCount $true
Show-LegacyNode "user_metrics         " $oldUserMetrics $oldUserMetricCount $true
Show-LegacyNode "licenses             " $licenses $licenseCount $false
Write-Host "  app_versions           : (protected - not touched)" -ForegroundColor Green
Write-Host "  admin_users            : (protected - not touched)" -ForegroundColor Green

# ============================================================
# MIGRATION PERCENTAGE
# ============================================================

Write-Host ""
Write-Host "--- Migration Progress ---" -ForegroundColor Cyan

# Compare: how many old heartbeat devices are now in the new devices/ node?
$oldDeviceIds = @()
$migratedDeviceIds = @()
$unmigratedDevices = @()

if ($oldHeartbeats -and $oldHeartbeats -ne "ACCESS_DENIED") {
    foreach ($prop in $oldHeartbeats.PSObject.Properties) {
        $oldDeviceIds += $prop.Name
        $hb = $prop.Value
        
        # Check if this device exists in new structure
        $inNewStructure = $false
        if ($newDevices -and $newDevices -ne "ACCESS_DENIED") {
            try {
                $match = $newDevices.PSObject.Properties[$prop.Name]
                if ($match) { $inNewStructure = $true }
            } catch {}
        }
        
        if ($inNewStructure) {
            $migratedDeviceIds += $prop.Name
        } else {
            $deviceName = try { $hb.device_name } catch { "Unknown" }
            $appId = try { $hb.app_id } catch { "?" }
            $appVersion = try { $hb.app_version } catch { "?" }
            $lastSeen = try { $hb.last_seen } catch { "?" }
            $status = try { $hb.status } catch { "?" }
            $unmigratedDevices += @{
                id = $prop.Name
                name = $deviceName
                app = $appId
                version = $appVersion
                last_seen = $lastSeen
                status = $status
            }
        }
    }
}

$totalOldDevices = $oldDeviceIds.Count
$migratedCount = $migratedDeviceIds.Count
$percentage = if ($totalOldDevices -gt 0) { [math]::Round(($migratedCount / $totalOldDevices) * 100, 1) } else { 100 }

$barWidth = 30
$filledWidth = [math]::Floor($barWidth * $percentage / 100)
$emptyWidth = $barWidth - $filledWidth
$progressBar = ("=" * $filledWidth) + ("-" * $emptyWidth)

Write-Host ""
Write-Host "  Device Migration:  [$progressBar] $percentage%" -ForegroundColor $(if ($percentage -ge 100) { "Green" } elseif ($percentage -ge 50) { "Yellow" } else { "Red" })
Write-Host "    $migratedCount / $totalOldDevices old devices now in new structure" -ForegroundColor Gray
Write-Host ""

if ($unmigratedDevices.Count -gt 0) {
    Write-Host "  Devices NOT yet migrated (need to launch v1.6.1+):" -ForegroundColor Yellow
    foreach ($dev in $unmigratedDevices) {
        $shortId = $dev.id.Substring(0, [Math]::Min(12, $dev.id.Length))
        $lastSeenShort = if ($dev.last_seen -and $dev.last_seen.Length -gt 19) { $dev.last_seen.Substring(0, 19) } else { $dev.last_seen }
        Write-Host "    $shortId... | $($dev.name) | $($dev.app) v$($dev.version) | $($dev.status) | last: $lastSeenShort" -ForegroundColor DarkYellow
    }
    Write-Host ""
}

# Check if any unmigrated devices are running old versions
$oldVersionDevices = $unmigratedDevices | Where-Object { $_.version -and $_.version -ne "1.6.1" -and $_.version -ne "1.6.0" }
$staleDevices = $unmigratedDevices | Where-Object { 
    try {
        $ls = [datetime]::Parse($_.last_seen)
        return ($ls -lt (Get-Date).AddDays(-30))
    } catch { return $true }
}

if ($staleDevices.Count -gt 0) {
    Write-Host "  Stale devices (not seen in 30+ days, likely inactive):" -ForegroundColor DarkGray
    foreach ($dev in $staleDevices) {
        Write-Host "    $($dev.name) ($($dev.app) v$($dev.version))" -ForegroundColor DarkGray
    }
    Write-Host ""
}

# ============================================================
# SAFETY ASSESSMENT
# ============================================================

Write-Host "--- Safety Assessment ---" -ForegroundColor Cyan
Write-Host ""

$safeToClean = $oldLaunchCount + $oldSessionCount + $oldErrorCount + $oldCheckCount + $oldHeartbeatCount + $oldActivationCount + $oldUsageLogCount + $oldUserMetricCount
Write-Host "  Total legacy records safe to wipe: $safeToClean" -ForegroundColor White
Write-Host "    device_heartbeats:     $oldHeartbeatCount" -ForegroundColor Gray
Write-Host "    device_activations:    $oldActivationCount" -ForegroundColor Gray
Write-Host "    app_launches:          $oldLaunchCount" -ForegroundColor Gray
Write-Host "    processing_sessions:   $oldSessionCount" -ForegroundColor Gray
Write-Host "    error_logs:            $oldErrorCount" -ForegroundColor Gray
Write-Host "    update_checks:         $oldCheckCount" -ForegroundColor Gray
Write-Host "    usage_logs:            $oldUsageLogCount" -ForegroundColor Gray
Write-Host "    user_metrics:          $oldUserMetricCount" -ForegroundColor Gray
Write-Host ""
Write-Host "  Apps no longer write to these nodes. All data goes to events/." -ForegroundColor Gray
Write-Host "  Wiping is safe. Devices will re-register on next launch." -ForegroundColor Gray
Write-Host ""

if ($percentage -lt 100 -and $unmigratedDevices.Count -gt 0) {
    Write-Host "  NOTE: $($unmigratedDevices.Count) device(s) haven't launched v1.6.1+ yet." -ForegroundColor Yellow
    Write-Host "  Wiping is still safe - they will repopulate both old and new" -ForegroundColor Yellow
    Write-Host "  nodes when they next launch with the updated version." -ForegroundColor Yellow
    Write-Host ""
}

# ============================================================
# CLEANUP (if -Clean flag)
# ============================================================

if ($Clean) {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  CLEANUP MODE" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Will DELETE the following legacy nodes:" -ForegroundColor Red
    Write-Host "    - device_heartbeats    ($oldHeartbeatCount entries)" -ForegroundColor Yellow
    Write-Host "    - device_activations   ($oldActivationCount entries)" -ForegroundColor Yellow
    Write-Host "    - app_launches         ($oldLaunchCount entries)" -ForegroundColor Yellow
    Write-Host "    - processing_sessions  ($oldSessionCount entries)" -ForegroundColor Yellow
    Write-Host "    - error_logs           ($oldErrorCount entries)" -ForegroundColor Yellow
    Write-Host "    - update_checks        ($oldCheckCount entries)" -ForegroundColor Yellow
    Write-Host "    - usage_logs           ($oldUsageLogCount entries)" -ForegroundColor Yellow
    Write-Host "    - user_metrics         ($oldUserMetricCount entries)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Will NOT touch:" -ForegroundColor Green
    Write-Host "    - app_versions (update system)" -ForegroundColor Green
    Write-Host "    - admin_users (admin flags)" -ForegroundColor Green
    Write-Host "    - licenses (license validation - source of truth)" -ForegroundColor Green
    Write-Host "    - users/ (new structure)" -ForegroundColor Green
    Write-Host "    - devices/ (new structure)" -ForegroundColor Green
    Write-Host "    - events/ (new structure)" -ForegroundColor Green
    Write-Host "    - errors/ (new structure)" -ForegroundColor Green
    Write-Host ""
    
    $confirm = Read-Host "  Type 'YES' to proceed with cleanup"
    
    if ($confirm -eq "YES") {
        Write-Host ""
        $nodes = @("device_heartbeats", "device_activations", "app_launches", "processing_sessions", "error_logs", "update_checks", "usage_logs", "user_metrics")
        foreach ($node in $nodes) {
            Write-Host "  Deleting $node..." -NoNewline -ForegroundColor Yellow
            $ok = Delete-FirebaseNode $node
            if ($ok) {
                Write-Host " done" -ForegroundColor Green
            } else {
                Write-Host " FAILED (may need auth)" -ForegroundColor Red
            }
        }
        Write-Host ""
        Write-Host "  Cleanup complete! Apps will repopulate on next launch." -ForegroundColor Green
        Write-Host ""
    } else {
        Write-Host ""
        Write-Host "  Cleanup cancelled." -ForegroundColor Gray
        Write-Host ""
    }
} else {
    Write-Host "  Run with -Clean flag to wipe legacy nodes:" -ForegroundColor Gray
    Write-Host "    .\migration-status.ps1 -Clean" -ForegroundColor Gray
    Write-Host ""
}
