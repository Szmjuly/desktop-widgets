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

# Enumerate every tenant and show counts for devices + licenses under each.
$tenants = fb-get "tenants"
if ($null -eq $tenants) {
    Write-Host "No tenants node found. Nothing to do." -ForegroundColor Yellow
    exit 0
}

$tenantIds = @($tenants.PSObject.Properties | ForEach-Object { $_.Name })
$plan = @()   # list of { tenant, kind, path, count }

foreach ($tid in $tenantIds) {
    $d = fb-get "tenants/$tid/devices"
    $dc = 0; if ($d) { try { $dc = @($d.PSObject.Properties).Count } catch {} }
    $plan += [pscustomobject]@{ Tenant=$tid; Kind="devices"; Path="tenants/$tid/devices"; Count=$dc }

    $l = fb-get "tenants/$tid/licenses"
    $lc = 0
    if ($l) {
        # two-level: licenses/{appId}/{key}
        foreach ($appProp in $l.PSObject.Properties) {
            try { $lc += @($appProp.Value.PSObject.Properties).Count } catch {}
        }
    }
    $plan += [pscustomobject]@{ Tenant=$tid; Kind="licenses"; Path="tenants/$tid/licenses"; Count=$lc }
}

Write-Host ""
foreach ($row in $plan) {
    Write-Host ("  {0,-20}  {1,-10} {2,6} entries" -f $row.Tenant, $row.Kind, $row.Count) -ForegroundColor Cyan
}
Write-Host ""
Write-Host "  Both apps recreate their device entry on next launch." -ForegroundColor Gray
Write-Host "  Renamer auto-creates a new free license on next launch." -ForegroundColor Gray
Write-Host ""

$total = ($plan | Measure-Object Count -Sum).Sum
if ($total -eq 0) {
    Write-Host "Nothing to wipe." -ForegroundColor Yellow
    exit 0
}

if (-not $Force) {
    $confirm = Read-Host "Type YES to wipe devices/ and licenses/ under ALL tenants"
    if ($confirm -ne "YES") {
        Write-Host "Cancelled." -ForegroundColor Gray
        exit 0
    }
}

foreach ($row in $plan) {
    if ($row.Count -eq 0) { continue }
    Write-Host ("  Deleting {0} ({1})..." -f $row.Path, $row.Count) -NoNewline -ForegroundColor Yellow
    try {
        fb-delete $row.Path
        Write-Host " done" -ForegroundColor Green
    } catch {
        Write-Host " FAILED: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Wiped. Apps will recreate entries on next launch." -ForegroundColor Green
Write-Host ""
