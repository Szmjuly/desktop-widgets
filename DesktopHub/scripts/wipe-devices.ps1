#!/usr/bin/env pwsh
# Wipe the devices/ node from Firebase.
# Both DesktopHub and Renamer recreate their entry on next launch.
#
# Usage:
#   .\wipe-devices.ps1              # Wipe devices/ (with confirmation)
#   .\wipe-devices.ps1 -Force       # Skip confirmation
#   .\wipe-devices.ps1 -ServiceAccountPath "path\to\sa.json"

param(
    [switch]$WipeLicenses,
    [switch]$Force,
    [string]$ServiceAccountPath
)

$baseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com"

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
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

function fb-get([string]$path) {
    try {
        $r = Invoke-RestMethod -Uri "$baseUrl/$path.json?access_token=$($script:token)" -Method Get -TimeoutSec 15
        if ($null -eq $r -or $r -eq "null") { return $null }
        return $r
    } catch { return $null }
}

function fb-delete([string]$path) {
    Invoke-RestMethod -Uri "$baseUrl/$path.json?access_token=$($script:token)" -Method Delete -TimeoutSec 15 | Out-Null
}

# Show current counts
$devices = fb-get "devices"
$devCount = 0
if ($devices) { try { $devCount = @($devices.PSObject.Properties).Count } catch {} }

$licData = fb-get "licenses"
$licCount = 0
if ($licData) { try { $licCount = @($licData.PSObject.Properties).Count } catch {} }

Write-Host ""
Write-Host "  devices/   : $devCount entries" -ForegroundColor Cyan
Write-Host "  licenses/  : $licCount entries" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Both apps recreate their device entry on next launch." -ForegroundColor Gray
Write-Host "  Renamer auto-creates a new free license on next launch." -ForegroundColor Gray
Write-Host ""

if ($devCount -eq 0 -and $licCount -eq 0) {
    Write-Host "Nothing to wipe." -ForegroundColor Yellow
    exit 0
}

if (-not $Force) {
    $prompt = "Type YES to wipe devices/ and licenses/"
    $confirm = Read-Host $prompt
    if ($confirm -ne "YES") {
        Write-Host "Cancelled." -ForegroundColor Gray
        exit 0
    }
}

if ($devCount -gt 0) {
    Write-Host "  Deleting devices/ ($devCount)..." -NoNewline -ForegroundColor Yellow
    try {
        fb-delete "devices"
        Write-Host " done" -ForegroundColor Green
    } catch {
        Write-Host " FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

if ($licCount -gt 0) {
    Write-Host "  Deleting licenses/ ($licCount)..." -NoNewline -ForegroundColor Yellow
    try {
        fb-delete "licenses"
        Write-Host " done" -ForegroundColor Green
    } catch {
        Write-Host " FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Wiped. Apps will recreate entries on next launch." -ForegroundColor Green
Write-Host ""
