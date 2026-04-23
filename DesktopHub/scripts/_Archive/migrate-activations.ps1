#!/usr/bin/env pwsh
# Device Activations Migration Tool
#
# Reads device_activations (legacy flat node) and migrates all data into the new
# linked structure: users/{username}/devices, devices/{id}, licenses/{key}/devices.
#
# After migration, device_activations can be safely wiped. Apps recreate their
# own entry on next launch (dual-write is still active in app code).
#
# Licenses are NEVER deleted — they are the source of truth for paid plans.
# This script only ADDS a devices/ sub-node to each license record.
#
# Usage:
#   .\migrate-activations.ps1                  # Preview only — shows what would change
#   .\migrate-activations.ps1 -Commit          # Execute migration
#   .\migrate-activations.ps1 -Commit -Clean   # Migrate then wipe device_activations
#   .\migrate-activations.ps1 -ServiceAccountPath "path\to\sa.json"

param(
    [switch]$Commit,
    [switch]$Clean,
    [string]$ServiceAccountPath
)

$baseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com"

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
            throw "Failed to load service account private key: $($_.Exception.Message)"
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
    exit 1
}

Write-Host "Authenticating..." -ForegroundColor Gray
try {
    $script:token = Get-ServiceAccountAccessToken $ServiceAccountPath
    Write-Host "OK." -ForegroundColor Green
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# ============================================================
# FIREBASE HELPERS
# ============================================================

function fb-get([string]$path) {
    try {
        $r = Invoke-RestMethod -Uri "$baseUrl/$path.json?access_token=$($script:token)" -Method Get -TimeoutSec 15
        if ($null -eq $r -or $r -eq "null") { return $null }
        return $r
    } catch { return $null }
}

function fb-put([string]$path, $value) {
    $body = if ($value -is [bool]) { $value.ToString().ToLower() } else { $value | ConvertTo-Json -Depth 10 -Compress }
    Invoke-RestMethod -Uri "$baseUrl/$path.json?access_token=$($script:token)" -Method Put -Body $body -ContentType "application/json" -TimeoutSec 15 | Out-Null
}

function fb-patch([string]$path, $value) {
    $body = $value | ConvertTo-Json -Depth 10 -Compress
    Invoke-RestMethod -Uri "$baseUrl/$path.json?access_token=$($script:token)" -Method Patch -Body $body -ContentType "application/json" -TimeoutSec 15 | Out-Null
}

function fb-delete([string]$path) {
    Invoke-RestMethod -Uri "$baseUrl/$path.json?access_token=$($script:token)" -Method Delete -TimeoutSec 15 | Out-Null
}

# ============================================================
# READ ALL RELEVANT NODES
# ============================================================

Write-Host ""
Write-Host "Reading Firebase data..." -ForegroundColor Cyan

$activations  = fb-get "device_activations"
$newDevices   = fb-get "devices"
$newUsers     = fb-get "users"
$licenses     = fb-get "licenses"

if ($null -eq $activations) {
    Write-Host ""
    Write-Host "device_activations is empty or missing - nothing to migrate." -ForegroundColor Yellow
    exit 0
}

$activationList = @($activations.PSObject.Properties)
$existingDeviceIds = if ($newDevices) { @($newDevices.PSObject.Properties | ForEach-Object { $_.Name }) } else { @() }

Write-Host "  device_activations: $($activationList.Count) entries" -ForegroundColor White
Write-Host "  devices/ (new):     $($existingDeviceIds.Count) entries" -ForegroundColor White
Write-Host "  users/ (new):       $(if ($newUsers) { @($newUsers.PSObject.Properties).Count } else { 0 }) entries" -ForegroundColor White
Write-Host "  licenses/:          $(if ($licenses) { @($licenses.PSObject.Properties).Count } else { 0 }) entries" -ForegroundColor White

# ============================================================
# PLAN MIGRATION
# ============================================================

Write-Host ""
Write-Host "--- Migration Plan ---" -ForegroundColor Cyan
Write-Host ""

$alreadyMigrated  = @()
$toMigrateDevices = @()  # Need to create/update in devices/
$toMigrateUsers   = @()  # Need to update in users/
$toLinkLicenses   = @()  # Need to add device link to licenses/

foreach ($prop in $activationList) {
    $deviceId   = $prop.Name
    $act        = $prop.Value
    $username   = try { $act.username } catch { $null }
    $licenseKey = try { $act.license_key } catch { $null }
    $appId      = try { $act.app_id } catch { "spec-updater" }
    $deviceName = try { $act.device_name } catch { "Unknown" }
    $appVersion = try { $act.app_version } catch { "1.0.0" }
    $activatedAt = try { $act.activated_at } catch { (Get-Date -Format 'o') }
    $lastVal    = try { $act.last_validated } catch { $activatedAt }

    $inNewDevices = $existingDeviceIds -contains $deviceId

    if ($inNewDevices) {
        $alreadyMigrated += $deviceId
    } else {
        $toMigrateDevices += @{
            deviceId    = $deviceId
            username    = $username
            licenseKey  = $licenseKey
            appId       = $appId
            deviceName  = $deviceName
            appVersion  = $appVersion
            activatedAt = $activatedAt
            lastVal     = $lastVal
        }
    }

    # Always ensure user->device link exists
    $userDeviceLinked = $false
    if ($username -and $newUsers) {
        try {
            $userNode = $newUsers.PSObject.Properties[$username]
            if ($userNode -and $userNode.Value.devices) {
                $userDeviceLinked = $null -ne $userNode.Value.devices.PSObject.Properties[$deviceId]
            }
        } catch {}
    }
    if (-not $userDeviceLinked -and $username) {
        $toMigrateUsers += @{ username = $username; deviceId = $deviceId }
    }

    # Always ensure license->device link exists
    $licDeviceLinked = $false
    if ($licenseKey -and $licenses) {
        try {
            $licNode = $licenses.PSObject.Properties[$licenseKey]
            if ($licNode -and $licNode.Value.devices) {
                $licDeviceLinked = $null -ne $licNode.Value.devices.PSObject.Properties[$deviceId]
            }
        } catch {}
    }
    if (-not $licDeviceLinked -and $licenseKey) {
        $toLinkLicenses += @{ licenseKey = $licenseKey; deviceId = $deviceId }
    }
}

# Report plan
Write-Host "  Already in new devices/ node:   $($alreadyMigrated.Count)  (no action needed)" -ForegroundColor Green
Write-Host "  Need to create in devices/:     $($toMigrateDevices.Count)" -ForegroundColor $(if ($toMigrateDevices.Count -gt 0) { "Yellow" } else { "Gray" })
Write-Host "  User->device links to add:      $($toMigrateUsers.Count)" -ForegroundColor $(if ($toMigrateUsers.Count -gt 0) { "Yellow" } else { "Gray" })
Write-Host "  License->device links to add:   $($toLinkLicenses.Count)" -ForegroundColor $(if ($toLinkLicenses.Count -gt 0) { "Yellow" } else { "Gray" })
Write-Host ""

if ($toMigrateDevices.Count -gt 0) {
    Write-Host "  Devices to create in devices/:" -ForegroundColor Yellow
    foreach ($d in $toMigrateDevices) {
        $shortId  = $d.deviceId.Substring(0, [Math]::Min(12, $d.deviceId.Length))
        $devName  = $d.deviceName
        $devUser  = $d.username
        $devApp   = $d.appId
        $devVer   = $d.appVersion
        $devKey   = $d.licenseKey
        Write-Host "    $shortId... | $devName | user=$devUser | app=$devApp v$devVer | key=$devKey" -ForegroundColor DarkYellow
    }
    Write-Host ""
}

# ============================================================
# EXECUTE MIGRATION
# ============================================================

if (-not $Commit) {
    Write-Host "Preview mode only. Run with -Commit to apply." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Final linked structure will be:" -ForegroundColor Cyan
    Write-Host "    users/{username}/devices/{deviceId}  = true" -ForegroundColor Gray
    Write-Host "    devices/{deviceId}/username          = '{username}'" -ForegroundColor Gray
    Write-Host "    devices/{deviceId}/license_key       = '{licenseKey}'" -ForegroundColor Gray
    Write-Host "    devices/{deviceId}/apps/{appId}/...  = { version, status }" -ForegroundColor Gray
    Write-Host "    licenses/{key}/devices/{deviceId}    = true" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Licenses are NOT deleted - only enriched with device links." -ForegroundColor Green
    exit 0
}

$now = (Get-Date -Format 'o')
$errors = 0
$done = 0

# 1. Create missing device entries
foreach ($d in $toMigrateDevices) {
    Write-Host "  Creating devices/$($d.deviceId.Substring(0,12))..." -NoNewline -ForegroundColor Yellow
    try {
        $devicePayload = @{
            device_name      = $d.deviceName
            username         = $d.username
            license_key      = $d.licenseKey
            last_seen        = $d.lastVal
            status           = "active"
            migrated_from    = "device_activations"
            migrated_at      = $now
        }
        fb-patch "devices/$($d.deviceId)" $devicePayload

        $appPayload = @{
            installed_version = $d.appVersion
            last_launch       = $d.lastVal
            status            = "active"
            license_key       = $d.licenseKey
            activated_at      = $d.activatedAt
        }
        fb-patch "devices/$($d.deviceId)/apps/$($d.appId)" $appPayload

        Write-Host " done" -ForegroundColor Green
        $done++
    } catch {
        Write-Host " FAILED: $($_.Exception.Message)" -ForegroundColor Red
        $errors++
    }
}

# 2. Add user->device links
foreach ($u in $toMigrateUsers) {
    Write-Host "  Linking users/$($u.username)/devices/$($u.deviceId.Substring(0,8))..." -NoNewline -ForegroundColor Yellow
    try {
        fb-put "users/$($u.username)/devices/$($u.deviceId)" $true
        Write-Host " done" -ForegroundColor Green
        $done++
    } catch {
        Write-Host " FAILED: $($_.Exception.Message)" -ForegroundColor Red
        $errors++
    }
}

# 3. Add license->device links
foreach ($l in $toLinkLicenses) {
    Write-Host "  Linking licenses/$($l.licenseKey)/devices/$($l.deviceId.Substring(0,8))..." -NoNewline -ForegroundColor Yellow
    try {
        fb-put "licenses/$($l.licenseKey)/devices/$($l.deviceId)" $true
        Write-Host " done" -ForegroundColor Green
        $done++
    } catch {
        Write-Host " FAILED: $($_.Exception.Message)" -ForegroundColor Red
        $errors++
    }
}

Write-Host ""
Write-Host "Migration complete. $done operation(s) succeeded, $errors failed." -ForegroundColor $(if ($errors -eq 0) { "Green" } else { "Yellow" })

# 4. Optionally wipe device_activations
if ($Clean) {
    if ($errors -gt 0) {
        Write-Host ""
        Write-Host "Skipping device_activations cleanup - $errors error(s) occurred during migration." -ForegroundColor Red
        Write-Host "Re-run with -Commit to retry, then use -Commit -Clean once all succeed." -ForegroundColor Yellow
    } else {
        Write-Host ""
        Write-Host "Deleting device_activations node..." -NoNewline -ForegroundColor Yellow
        try {
            fb-delete "device_activations"
            Write-Host " done" -ForegroundColor Green
            Write-Host ""
            Write-Host "All $($activationList.Count) activation(s) migrated. device_activations wiped." -ForegroundColor Green
            Write-Host "Apps will recreate their own entry on next launch (dual-write still active)." -ForegroundColor Gray
        } catch {
            Write-Host " FAILED: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
} else {
    Write-Host ""
    Write-Host "device_activations NOT wiped. Run with -Commit -Clean to wipe after verifying." -ForegroundColor Gray
}

Write-Host ""
Write-Host "Final structure:" -ForegroundColor Cyan
Write-Host "  users/{username}/devices/{deviceId}  = true" -ForegroundColor Gray
Write-Host "  devices/{deviceId}/username          = '{username}'" -ForegroundColor Gray
Write-Host "  devices/{deviceId}/license_key       = '{licenseKey}'" -ForegroundColor Gray
Write-Host "  devices/{deviceId}/apps/{appId}/...  = { version, status, activated_at }" -ForegroundColor Gray
Write-Host "  licenses/{key}/devices/{deviceId}    = true" -ForegroundColor Gray
Write-Host ""
