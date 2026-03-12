#!/usr/bin/env pwsh
# Firebase Database Backup Tool
# Backs up the entire database or specific collections to JSON files.
#
# Usage:
#   .\backup-database.ps1                                    # Backup entire database
#   .\backup-database.ps1 -Collections "devices,users"       # Backup specific collections
#   .\backup-database.ps1 -Collections "project_tags"        # Backup just tags
#   .\backup-database.ps1 -OutputDir "C:\backups"            # Custom output directory
#   .\backup-database.ps1 -ServiceAccountPath "path\to\sa.json"

param(
    [string]$Collections,
    [string]$OutputDir,
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

function fb-get-raw([string]$path) {
    $url = "$baseUrl/$path.json?access_token=$($script:token)"
    try {
        $response = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 60
        return $response.Content
    } catch {
        return $null
    }
}

# ============================================================
# BACKUP
# ============================================================

# Setup output directory
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $PSScriptRoot "backups"
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$timestamp = (Get-Date).ToString("yyyy-MM-dd_HHmmss")

Write-Host ""
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host "       Firebase Database Backup" -ForegroundColor White
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrWhiteSpace($Collections)) {
    # Full database backup
    Write-Host "  Backing up entire database..." -ForegroundColor Yellow
    $json = fb-get-raw ""
    if ($null -eq $json -or $json -eq "null") {
        Write-Host "  Database is empty or inaccessible." -ForegroundColor Red
        exit 1
    }

    $outFile = Join-Path $OutputDir "firebase_full_$timestamp.json"
    # Pretty-print the JSON
    try {
        $parsed = $json | ConvertFrom-Json
        $pretty = $parsed | ConvertTo-Json -Depth 20
        [System.IO.File]::WriteAllText($outFile, $pretty)
    } catch {
        # Fallback: write raw if parse fails
        [System.IO.File]::WriteAllText($outFile, $json)
    }

    $sizeKb = [Math]::Round((Get-Item $outFile).Length / 1024, 1)
    Write-Host "  Saved: $outFile ($sizeKb KB)" -ForegroundColor Green
} else {
    # Specific collections
    $collectionList = $Collections.Split(',', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() }

    Write-Host "  Backing up $($collectionList.Count) collection(s): $($collectionList -join ', ')" -ForegroundColor Yellow
    Write-Host ""

    foreach ($col in $collectionList) {
        Write-Host "  $col... " -NoNewline -ForegroundColor White
        $json = fb-get-raw $col
        if ($null -eq $json -or $json -eq "null") {
            Write-Host "empty/not found" -ForegroundColor DarkGray
            continue
        }

        $outFile = Join-Path $OutputDir "firebase_${col}_$timestamp.json"
        try {
            $parsed = $json | ConvertFrom-Json
            $pretty = $parsed | ConvertTo-Json -Depth 20
            [System.IO.File]::WriteAllText($outFile, $pretty)
        } catch {
            [System.IO.File]::WriteAllText($outFile, $json)
        }

        $sizeKb = [Math]::Round((Get-Item $outFile).Length / 1024, 1)
        Write-Host "saved ($sizeKb KB)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "  Backup complete. Output: $OutputDir" -ForegroundColor Cyan
Write-Host ""
