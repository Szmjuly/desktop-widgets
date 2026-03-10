#!/usr/bin/env pwsh
# Quick version updater for DesktopHub
# Usage: .\update-version.ps1 "1.7.0" "Bug fixes and improvements"
#        .\update-version.ps1 "1.7.0" "Bug fixes" -ServiceAccountPath "path\to\sa.json"

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Version,

    [Parameter(Mandatory=$false, Position=1)]
    [string]$ReleaseNotes = "New version available",

    [Parameter(Mandatory=$false)]
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

$candidates = @()
if (-not [string]::IsNullOrWhiteSpace($ServiceAccountPath)) {
    $candidates += $ServiceAccountPath
}

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

$candidates += @(
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

Write-Host "Authenticating with service account..." -ForegroundColor Gray
try {
    $token = Get-ServiceAccountAccessToken $ServiceAccountPath
} catch {
    $errMsg = $_.Exception.Message
    Write-Host "Auth failed: $errMsg" -ForegroundColor Red
    exit 1
}

# ============================================================
# PUSH VERSION
# ============================================================

$data = @{
    latest_version = $Version
    release_date = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    release_notes = $ReleaseNotes
    download_url = "https://github.com/Szmjuly/desktop-widgets/releases/download/v$Version/DesktopHub.exe"
    required_update = $false
    updated_at = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
} | ConvertTo-Json

$url = "$baseUrl/app_versions/desktophub.json?access_token=$token"

Write-Host "Updating DesktopHub to v$Version..." -ForegroundColor Cyan

try {
    Invoke-RestMethod -Uri $url -Method Put -Body $data -ContentType "application/json" | Out-Null
    Write-Host "Success! Version $Version is now live." -ForegroundColor Green
    Write-Host "View: https://console.firebase.google.com/project/licenses-ff136/database" -ForegroundColor Gray
} catch {
    $errMsg = $_.Exception.Message
    Write-Host "Error: $errMsg" -ForegroundColor Red
}
