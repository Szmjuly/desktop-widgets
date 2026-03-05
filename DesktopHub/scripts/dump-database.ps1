#!/usr/bin/env pwsh
# Firebase Database Structure Dump
# Reads the entire database and displays the tree structure with entry counts.
#
# Usage:
#   .\dump-database.ps1                          # Show structure with counts
#   .\dump-database.ps1 -Full                    # Show all data (verbose)
#   .\dump-database.ps1 -WipeDevices             # Wipe devices/ node after showing structure
#   .\dump-database.ps1 -ServiceAccountPath "path\to\sa.json"

param(
    [switch]$Full,
    [switch]$WipeDevices,
    [string]$ServiceAccountPath
)

$baseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com"

# ============================================================
# SERVICE ACCOUNT AUTH (same as other scripts)
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

# ============================================================
# SERVICE ACCOUNT DISCOVERY
# ============================================================

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

# ============================================================
# HELPERS
# ============================================================

function fb-get([string]$path) {
    try {
        $url = "$baseUrl/$path.json?access_token=$($script:token)"
        $r = Invoke-RestMethod -Uri $url -Method Get -TimeoutSec 30
        if ($null -eq $r -or $r -eq "null") { return $null }
        return $r
    } catch { return $null }
}

function fb-delete([string]$path) {
    $url = "$baseUrl/$path.json?access_token=$($script:token)"
    Invoke-RestMethod -Uri $url -Method Delete -TimeoutSec 15 | Out-Null
}

function Count-Children($node) {
    if ($null -eq $node) { return 0 }
    try { return @($node.PSObject.Properties).Count } catch { return 0 }
}

function Show-Tree($node, [string]$indent = "", [int]$maxDepth = 3, [int]$depth = 0) {
    if ($null -eq $node) { return }
    if ($depth -ge $maxDepth) {
        Write-Host "${indent}..." -ForegroundColor DarkGray
        return
    }

    try {
        $props = @($node.PSObject.Properties)
    } catch {
        Write-Host "${indent}$node" -ForegroundColor Gray
        return
    }

    foreach ($prop in $props) {
        $key = $prop.Name
        $val = $prop.Value

        $isLeaf = $false
        $childCount = 0
        try {
            $childProps = @($val.PSObject.Properties)
            $childCount = $childProps.Count
        } catch {
            $isLeaf = $true
        }

        if ($isLeaf -or $childCount -eq 0) {
            $displayVal = "$val"
            if ($displayVal.Length -gt 80) { $displayVal = $displayVal.Substring(0, 77) + "..." }
            Write-Host "${indent}$key = " -NoNewline -ForegroundColor White
            Write-Host "$displayVal" -ForegroundColor DarkGray
        } else {
            Write-Host "${indent}$key/ " -NoNewline -ForegroundColor Cyan
            Write-Host "($childCount entries)" -ForegroundColor DarkYellow
            if ($Full -or $depth -lt 1) {
                Show-Tree $val "$indent  " $maxDepth ($depth + 1)
            }
        }
    }
}

# ============================================================
# READ AND DISPLAY
# ============================================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Firebase Database Structure Dump" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Reading entire database..." -ForegroundColor Gray

$root = fb-get ""

if ($null -eq $root) {
    Write-Host "Database is empty or inaccessible." -ForegroundColor Yellow
    exit 0
}

$topNodes = @($root.PSObject.Properties)

Write-Host ""
Write-Host "--- Top-Level Nodes ($($topNodes.Count)) ---" -ForegroundColor Green
Write-Host ""

foreach ($node in $topNodes) {
    $name = $node.Name
    $data = $node.Value
    $count = Count-Children $data

    if ($count -gt 0) {
        Write-Host "  $name/ " -NoNewline -ForegroundColor Cyan
        Write-Host "($count entries)" -ForegroundColor DarkYellow
    } else {
        $displayVal = "$data"
        if ($displayVal.Length -gt 60) { $displayVal = $displayVal.Substring(0, 57) + "..." }
        Write-Host "  $name = $displayVal" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "--- Detailed View ---" -ForegroundColor Green
Write-Host ""

$maxDepth = if ($Full) { 5 } else { 2 }

foreach ($node in $topNodes) {
    $name = $node.Name
    $data = $node.Value
    $count = Count-Children $data

    Write-Host "$name/ " -NoNewline -ForegroundColor Cyan
    Write-Host "($count entries)" -ForegroundColor DarkYellow

    if ($count -gt 0) {
        Show-Tree $data "  " $maxDepth 0
    }
    Write-Host ""
}

# ============================================================
# OPTIONAL: WIPE DEVICES
# ============================================================

if ($WipeDevices) {
    Write-Host "========================================" -ForegroundColor Red
    Write-Host "  WIPE DEVICES" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""

    $devCount = Count-Children (fb-get "devices")
    Write-Host "  devices/ has $devCount entries." -ForegroundColor Yellow
    Write-Host "  Both DesktopHub and Renamer recreate their entry on next launch." -ForegroundColor Gray
    Write-Host ""

    $confirm = Read-Host "  Type 'YES' to wipe devices/"
    if ($confirm -eq "YES") {
        Write-Host "  Deleting devices/..." -NoNewline -ForegroundColor Yellow
        try {
            fb-delete "devices"
            Write-Host " done" -ForegroundColor Green
        } catch {
            Write-Host " FAILED: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "  Cancelled." -ForegroundColor Gray
    }
    Write-Host ""
}
