#!/usr/bin/env pwsh
# Remote Force-Update Manager for DesktopHub
# Push updates to specific devices or all outdated devices.
#
# Usage:
#   .\push-update.ps1 -Action list                           # List all devices & versions
#   .\push-update.ps1 -Action push -DeviceId "abc-123"       # Push update to one device
#   .\push-update.ps1 -Action push-all                       # Push to all outdated devices
#   .\push-update.ps1 -Action status                         # Check push-update status
#   .\push-update.ps1 -Action clear                          # Clear completed/failed entries
#   .\push-update.ps1 -ServiceAccountPath "path\to\sa.json"

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("list", "push", "push-all", "status", "clear")]
    [string]$Action,

    [string]$DeviceId,
    [string]$ServiceAccountPath,

    # When set for -Action push (single device), force_update.target_version uses this instead of app_versions latest (download_url unchanged).
    [string]$TargetVersion,

    # Must match Firebase app_versions/{AppId} and devices/*/apps/{AppId}
    [string]$AppId = "desktophub"
)

$baseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com"
$appId = $AppId

# ============================================================
# SERVICE ACCOUNT AUTH
# ============================================================

function ConvertTo-Base64Url([byte[]]$bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Get-ServiceAccountAccessToken([string]$jsonPath) {
    $sa = Get-Content $jsonPath -Raw | ConvertFrom-Json
    $email = $sa.client_email
    $privateKeyPem = $sa.private_key

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

    $keyText = $privateKeyPem -replace '-----BEGIN PRIVATE KEY-----', '' -replace '-----END PRIVATE KEY-----', '' -replace '\s+', ''
    $keyBytes = [Convert]::FromBase64String($keyText)

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
            throw "Failed to load service account private key"
        }
    }

    $sigBytes = $rsa.SignData(
        [System.Text.Encoding]::UTF8.GetBytes($unsigned),
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
    )
    $sigB64 = ConvertTo-Base64Url($sigBytes)
    $jwt = "$unsigned.$sigB64"

    $response = Invoke-RestMethod -Uri "https://oauth2.googleapis.com/token" -Method Post -Body @{
        grant_type = "urn:ietf:params:oauth:grant-type:jwt-bearer"
        assertion  = $jwt
    } -ContentType "application/x-www-form-urlencoded" -TimeoutSec 15

    return $response.access_token
}

# ============================================================
# SERVICE ACCOUNT DISCOVERY
# ============================================================

if ([string]::IsNullOrWhiteSpace($ServiceAccountPath)) {
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
    try { $scriptDir = (Resolve-Path $scriptDir).Path } catch {}

    $candidates = @(
        (Join-Path $scriptDir "..\secrets\firebase-license.json"),
        (Join-Path $scriptDir "firebase-license.json"),
        (Join-Path (Split-Path -Parent $scriptDir) "secrets\firebase-license.json"),
        (Join-Path $PWD.Path "DesktopHub\secrets\firebase-license.json"),
        (Join-Path $PWD.Path "secrets\firebase-license.json"),
        (Join-Path $PWD.Path "firebase-license.json")
    )
    $candidates = @($candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)

    foreach ($c in $candidates) {
        try {
            if (Test-Path $c) {
                $ServiceAccountPath = (Resolve-Path $c).Path
                break
            }
        } catch {}
    }
}

if ([string]::IsNullOrWhiteSpace($ServiceAccountPath) -or -not (Test-Path $ServiceAccountPath)) {
    Write-Host "Error: Service account JSON not found." -ForegroundColor Red
    Write-Host "Looked in:" -ForegroundColor Yellow
    foreach ($candidate in $candidates) {
        Write-Host "  $candidate" -ForegroundColor Yellow
    }
    exit 1
}

# ============================================================
# AUTHENTICATE
# ============================================================

Write-Host "Authenticating..." -ForegroundColor Gray
try {
    $script:token = Get-ServiceAccountAccessToken $ServiceAccountPath
} catch {
    Write-Host "Auth failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# ============================================================
# FIREBASE HELPERS
# ============================================================

function fb-get([string]$path) {
    try {
        $url = "$baseUrl/$path.json?access_token=$($script:token)"
        $r = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 30
        if ($null -eq $r -or $r -eq "null") { return $null }
        return $r
    } catch { return $null }
}

function fb-put([string]$path, $data) {
    $url = "$baseUrl/$path.json?access_token=$($script:token)"
    $body = $data | ConvertTo-Json -Depth 10 -Compress
    Invoke-RestMethod -Uri $url -Method Put -Body $body -ContentType "application/json" -TimeoutSec 15 | Out-Null
}

function fb-patch([string]$path, $data) {
    $url = "$baseUrl/$path.json?access_token=$($script:token)"
    $body = $data | ConvertTo-Json -Depth 10 -Compress
    Invoke-RestMethod -Uri $url -Method Patch -Body $body -ContentType "application/json" -TimeoutSec 15 | Out-Null
}

function fb-delete([string]$path) {
    $url = "$baseUrl/$path.json?access_token=$($script:token)"
    Invoke-RestMethod -Uri $url -Method Delete -TimeoutSec 15 | Out-Null
}

# ============================================================
# GET LATEST VERSION INFO
# ============================================================

function Get-LatestVersionInfo {
    $versionData = fb-get "app_versions/$appId"
    if ($null -eq $versionData) {
        Write-Host "Error: Could not read app_versions/$appId" -ForegroundColor Red
        exit 1
    }
    return $versionData
}

function Is-Outdated([string]$installed, [string]$latest) {
    try {
        $iv = [Version]$installed
        $lv = [Version]$latest
        return $lv -gt $iv
    } catch {
        return $false
    }
}

# ============================================================
# ACTIONS
# ============================================================

switch ($Action) {
    "list" {
        Write-Host ""
        Write-Host "  ============================================" -ForegroundColor Cyan
        Write-Host "       Devices & Versions" -ForegroundColor White
        Write-Host "  ============================================" -ForegroundColor Cyan
        Write-Host ""

        $versionInfo = Get-LatestVersionInfo
        $latestVersion = $versionInfo.latest_version
        Write-Host "  Latest version: $latestVersion" -ForegroundColor Green
        Write-Host ""

        $devices = fb-get "devices"
        if ($null -eq $devices) {
            Write-Host "  No devices found." -ForegroundColor Yellow
            exit 0
        }

        $rows = @()
        foreach ($prop in $devices.PSObject.Properties) {
            $did = $prop.Name
            $dev = $prop.Value

            $username = "?"
            $deviceName = "?"
            $installedVersion = "?"
            $lastSeen = "?"
            $status = "?"

            try { $username = $dev.username } catch {}
            try { $deviceName = $dev.device_name } catch {}
            try { $lastSeen = $dev.last_seen } catch {}

            # Get app-specific info
            try {
                $appInfo = $dev.apps.$appId
                if ($appInfo) {
                    $installedVersion = $appInfo.installed_version
                    $status = $appInfo.status
                }
            } catch {}

            $outdated = Is-Outdated $installedVersion $latestVersion
            $versionDisplay = $installedVersion
            if ($outdated) { $versionDisplay = "$installedVersion  [OUTDATED]" }

            $rows += [PSCustomObject]@{
                DeviceId   = $did.Substring(0, [Math]::Min(12, $did.Length)) + "..."
                FullId     = $did
                User       = $username
                Device     = $deviceName
                Version    = $versionDisplay
                Outdated   = $outdated
                LastSeen   = if ($lastSeen -and $lastSeen -ne "?") { try { ([DateTime]$lastSeen).ToString("yyyy-MM-dd HH:mm") } catch { $lastSeen } } else { "?" }
            }
        }

        # Sort: outdated first, then by username
        $rows = $rows | Sort-Object -Property @{Expression={$_.Outdated}; Descending=$true}, User

        $outdatedCount = ($rows | Where-Object { $_.Outdated }).Count

        foreach ($row in $rows) {
            $color = if ($row.Outdated) { "Yellow" } else { "Gray" }
            Write-Host ("  {0,-16} {1,-12} {2,-14} {3,-24} {4}" -f $row.DeviceId, $row.User, $row.Device, $row.Version, $row.LastSeen) -ForegroundColor $color
        }

        Write-Host ""
        Write-Host "  Total: $($rows.Count) devices, $outdatedCount outdated" -ForegroundColor Cyan

        # Show full device IDs for outdated devices (needed for push)
        if ($outdatedCount -gt 0) {
            Write-Host ""
            Write-Host "  Outdated device IDs (for push):" -ForegroundColor Yellow
            foreach ($row in ($rows | Where-Object { $_.Outdated })) {
                Write-Host "    $($row.User): $($row.FullId)" -ForegroundColor Yellow
            }
        }
        Write-Host ""
    }

    "push" {
        if ([string]::IsNullOrWhiteSpace($DeviceId)) {
            Write-Host "Error: -DeviceId is required for push action." -ForegroundColor Red
            Write-Host "Run: .\push-update.ps1 -Action list  to see device IDs." -ForegroundColor Yellow
            exit 1
        }

        # Verify device exists
        $device = fb-get "devices/$DeviceId"
        if ($null -eq $device) {
            Write-Host "Error: Device '$DeviceId' not found in Firebase." -ForegroundColor Red
            exit 1
        }

        $versionInfo = Get-LatestVersionInfo
        $latestVersion = $versionInfo.latest_version
        $downloadUrl = $versionInfo.download_url

        if (-not [string]::IsNullOrWhiteSpace($TargetVersion)) {
            $latestVersion = $TargetVersion.Trim()
        }

        $username = try { $device.username } catch { "unknown" }
        $installedVersion = try { $device.apps.$appId.installed_version } catch { "unknown" }

        if (-not (Is-Outdated $installedVersion $latestVersion)) {
            Write-Host "  Device '$DeviceId' ($username) is already on v$installedVersion (target: $latestVersion)." -ForegroundColor Green
            exit 0
        }

        Write-Host ""
        Write-Host "  Pushing update to device:" -ForegroundColor Cyan
        Write-Host "    Device:    $DeviceId" -ForegroundColor White
        Write-Host "    User:      $username" -ForegroundColor White
        Write-Host "    Current:   $installedVersion" -ForegroundColor Yellow
        Write-Host "    Target:    $latestVersion" -ForegroundColor Green
        Write-Host ""

        $now = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        $pushData = @{
            app_id            = $appId
            target_version    = $latestVersion
            download_url      = $downloadUrl
            pushed_by         = $env:USERNAME.ToLower()
            pushed_at         = $now
            status            = "pending"
            status_updated_at = $now
            retry_count       = 0
        }

        fb-put "force_update/$DeviceId" $pushData
        Write-Host "  Done! Update pushed (status: pending)." -ForegroundColor Green
        Write-Host "  The device will pick it up on next heartbeat check." -ForegroundColor Gray
        Write-Host ""
    }

    "push-all" {
        $versionInfo = Get-LatestVersionInfo
        $latestVersion = $versionInfo.latest_version
        $downloadUrl = $versionInfo.download_url

        $devices = fb-get "devices"
        if ($null -eq $devices) {
            Write-Host "  No devices found." -ForegroundColor Yellow
            exit 0
        }

        $outdated = @()
        foreach ($prop in $devices.PSObject.Properties) {
            $did = $prop.Name
            $dev = $prop.Value
            $installedVersion = "0.0.0"
            try { $installedVersion = $dev.apps.$appId.installed_version } catch {}
            if (Is-Outdated $installedVersion $latestVersion) {
                $username = try { $dev.username } catch { "unknown" }
                $outdated += @{ Id = $did; User = $username; Version = $installedVersion }
            }
        }

        if ($outdated.Count -eq 0) {
            Write-Host "  All devices are up to date (v$latestVersion)." -ForegroundColor Green
            exit 0
        }

        Write-Host ""
        Write-Host "  Found $($outdated.Count) outdated device(s):" -ForegroundColor Yellow
        foreach ($d in $outdated) {
            Write-Host "    $($d.User): v$($d.Version) -> v$latestVersion" -ForegroundColor Yellow
        }
        Write-Host ""

        $confirm = Read-Host "  Type 'PUSH' to push update to all $($outdated.Count) device(s)"
        if ($confirm -ne "PUSH") {
            Write-Host "  Cancelled." -ForegroundColor Gray
            exit 0
        }

        $now = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        $pushed = 0
        foreach ($d in $outdated) {
            try {
                $pushData = @{
                    app_id            = $appId
                    target_version    = $latestVersion
                    download_url      = $downloadUrl
                    pushed_by         = $env:USERNAME.ToLower()
                    pushed_at         = $now
                    status            = "pending"
                    status_updated_at = $now
                    retry_count       = 0
                }
                fb-put "force_update/$($d.Id)" $pushData
                $pushed++
                Write-Host "    Pushed to $($d.User) ($($d.Id.Substring(0,8))...)" -ForegroundColor Green
            } catch {
                Write-Host "    FAILED: $($d.User) - $($_.Exception.Message)" -ForegroundColor Red
            }
        }

        Write-Host ""
        Write-Host "  Pushed to $pushed / $($outdated.Count) devices." -ForegroundColor Cyan
        Write-Host ""
    }

    "status" {
        Write-Host ""
        Write-Host "  ============================================" -ForegroundColor Cyan
        Write-Host "       Force-Update Status" -ForegroundColor White
        Write-Host "  ============================================" -ForegroundColor Cyan
        Write-Host ""

        $forceUpdates = fb-get "force_update"
        if ($null -eq $forceUpdates) {
            Write-Host "  No force-update entries." -ForegroundColor Gray
            Write-Host ""
            exit 0
        }

        foreach ($prop in $forceUpdates.PSObject.Properties) {
            $did = $prop.Name
            $entry = $prop.Value

            $entryApp = try { $entry.app_id } catch { "" }
            if (-not [string]::IsNullOrWhiteSpace($entryApp) -and $entryApp -ne $appId) { continue }

            $status = try { $entry.status } catch { "?" }
            $targetVersion = try { $entry.target_version } catch { "?" }
            $pushedBy = try { $entry.pushed_by } catch { "?" }
            $pushedAt = try { $entry.pushed_at } catch { "?" }
            $retryCount = try { $entry.retry_count } catch { 0 }
            $error_msg = try { $entry.error } catch { "" }
            $statusUpdatedAt = try { $entry.status_updated_at } catch { "" }

            # Look up username from devices node
            $device = fb-get "devices/$did"
            $username = try { $device.username } catch { "?" }

            $statusColor = switch ($status) {
                "pending"     { "Yellow" }
                "downloading" { "Cyan" }
                "installing"  { "Blue" }
                "completed"   { "Green" }
                "failed"      { "Red" }
                default       { "Gray" }
            }

            Write-Host "  $($did.Substring(0, [Math]::Min(12, $did.Length)))... ($username)" -ForegroundColor White
            Write-Host "    Status:  $status" -ForegroundColor $statusColor
            Write-Host "    Target:  v$targetVersion" -ForegroundColor Gray
            Write-Host "    Pushed:  $pushedAt by $pushedBy" -ForegroundColor Gray
            if ($retryCount -gt 0) {
                Write-Host "    Retries: $retryCount" -ForegroundColor Yellow
            }
            if (-not [string]::IsNullOrWhiteSpace($error_msg)) {
                Write-Host "    Error:   $error_msg" -ForegroundColor Red
            }
            Write-Host ""
        }
    }

    "clear" {
        Write-Host ""
        $forceUpdates = fb-get "force_update"
        if ($null -eq $forceUpdates) {
            Write-Host "  No force-update entries to clear." -ForegroundColor Gray
            Write-Host ""
            exit 0
        }

        $cleared = 0
        foreach ($prop in $forceUpdates.PSObject.Properties) {
            $did = $prop.Name
            $entry = $prop.Value
            $entryApp = try { $entry.app_id } catch { "" }
            if (-not [string]::IsNullOrWhiteSpace($entryApp) -and $entryApp -ne $appId) { continue }

            $status = try { $entry.status } catch { "" }

            if ($status -eq "completed" -or $status -eq "failed") {
                fb-delete "force_update/$did"
                $cleared++
                Write-Host "  Cleared: $($did.Substring(0,8))... (was: $status)" -ForegroundColor Green
            }
        }

        if ($cleared -eq 0) {
            Write-Host "  No completed/failed entries to clear." -ForegroundColor Gray
        } else {
            Write-Host ""
            Write-Host "  Cleared $cleared entries." -ForegroundColor Cyan
        }
        Write-Host ""
    }
}
