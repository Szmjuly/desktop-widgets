#!/usr/bin/env pwsh
# Wipe Tagging Data — dedicated script for wiping project tags, vocabulary, and registry.
# Separate from the main wipe script to prevent accidental tag data loss.
#
# Usage:
#   .\wipe-tags.ps1                          # Interactive — choose what to wipe
#   .\wipe-tags.ps1 -All                     # Wipe all tagging data (project_tags + tag_vocabulary + tag_registry)
#   .\wipe-tags.ps1 -TagsOnly               # Wipe only project_tags
#   .\wipe-tags.ps1 -VocabOnly              # Wipe only tag_vocabulary
#   .\wipe-tags.ps1 -RegistryOnly           # Wipe only tag_registry
#   .\wipe-tags.ps1 -All -Force             # Skip confirmation
#   .\wipe-tags.ps1 -ServiceAccountPath "path\to\sa.json"

param(
    [switch]$All,
    [switch]$TagsOnly,
    [switch]$VocabOnly,
    [switch]$RegistryOnly,
    [switch]$Force,
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

function Wipe-Node([string]$nodeName) {
    $data = fb-get $nodeName
    $count = Count-Children $data
    if ($count -eq 0 -and $null -eq $data) {
        Write-Host "  $nodeName/ — empty, skipping" -ForegroundColor DarkGray
        return
    }
    Write-Host "  Deleting $nodeName/ ($count entries)..." -NoNewline -ForegroundColor Yellow
    try {
        fb-delete $nodeName
        Write-Host " done" -ForegroundColor Green
    } catch {
        Write-Host " FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Clear-LocalCaches {
    $appData = Join-Path $env:LOCALAPPDATA "DesktopHub"
    $cacheFiles = @("tag_cache.json", "tag_vocabulary_cache.json")
    foreach ($file in $cacheFiles) {
        $path = Join-Path $appData $file
        if (Test-Path $path) {
            Remove-Item $path -Force
            Write-Host "  Cleared local cache: $file" -ForegroundColor Green
        }
    }
}

# ============================================================
# SHOW CURRENT STATE
# ============================================================

Write-Host ""
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host "       Wipe Tagging Data" -ForegroundColor White
Write-Host "  ============================================" -ForegroundColor Cyan
Write-Host ""

$tagCount = Count-Children (fb-get "project_tags")
$vocabCount = Count-Children (fb-get "tag_vocabulary")
$registryCount = Count-Children (fb-get "tag_registry")

Write-Host "  Current state:" -ForegroundColor Gray
Write-Host "    project_tags/   — $tagCount entries (HMAC-hashed project tag sets)" -ForegroundColor White
Write-Host "    tag_vocabulary/  — $vocabCount entries (shared suggested values)" -ForegroundColor White
Write-Host "    tag_registry/   — $registryCount entries (custom tag key names)" -ForegroundColor White
Write-Host ""

# ============================================================
# DETERMINE WHAT TO WIPE
# ============================================================

$wipeTags = $false
$wipeVocab = $false
$wipeRegistry = $false

if ($All) {
    $wipeTags = $true
    $wipeVocab = $true
    $wipeRegistry = $true
} elseif ($TagsOnly) {
    $wipeTags = $true
} elseif ($VocabOnly) {
    $wipeVocab = $true
} elseif ($RegistryOnly) {
    $wipeRegistry = $true
} else {
    # Interactive mode
    Write-Host "  What would you like to wipe?" -ForegroundColor Yellow
    Write-Host "    [1] All tagging data (project_tags + tag_vocabulary + tag_registry)"
    Write-Host "    [2] project_tags only (per-project tag values)"
    Write-Host "    [3] tag_vocabulary only (shared suggested values)"
    Write-Host "    [4] tag_registry only (custom tag key names)"
    Write-Host "    [Q] Cancel"
    Write-Host ""
    $choice = Read-Host "  Select"

    switch ($choice.ToUpper()) {
        "1" { $wipeTags = $true; $wipeVocab = $true; $wipeRegistry = $true }
        "2" { $wipeTags = $true }
        "3" { $wipeVocab = $true }
        "4" { $wipeRegistry = $true }
        "Q" { Write-Host "  Cancelled." -ForegroundColor Gray; exit 0 }
        default { Write-Host "  Invalid choice." -ForegroundColor Red; exit 1 }
    }
}

# Summarize what will be wiped
$targets = @()
if ($wipeTags) { $targets += "project_tags ($tagCount)" }
if ($wipeVocab) { $targets += "tag_vocabulary ($vocabCount)" }
if ($wipeRegistry) { $targets += "tag_registry ($registryCount)" }

Write-Host ""
Write-Host "  Will wipe: $($targets -join ', ')" -ForegroundColor Red
Write-Host "  Local tag caches will also be cleared." -ForegroundColor Red
Write-Host ""

if (-not $Force) {
    $confirm = Read-Host "  Type 'WIPE' to confirm"
    if ($confirm -ne "WIPE") {
        Write-Host "  Cancelled." -ForegroundColor Gray
        exit 0
    }
}

Write-Host ""

if ($wipeTags) { Wipe-Node "project_tags" }
if ($wipeVocab) { Wipe-Node "tag_vocabulary" }
if ($wipeRegistry) { Wipe-Node "tag_registry" }

Write-Host ""
Clear-LocalCaches

Write-Host ""
Write-Host "  Tag wipe complete." -ForegroundColor Cyan
Write-Host "  Tags will be recreated when users save project info." -ForegroundColor Gray
Write-Host ""
