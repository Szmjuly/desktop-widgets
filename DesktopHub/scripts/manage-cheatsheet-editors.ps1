#!/usr/bin/env pwsh
# Cheat Sheet Editor management for DesktopHub
# Manages cheat_sheet_editors in Firebase Realtime Database by Windows username.
# Editor role is independent from admin — edit access = admin OR editor.
#
# Authentication: Uses service account JSON (same credentials as DesktopHub).
#
# Usage:
#   .\manage-cheatsheet-editors.ps1 add <username>
#   .\manage-cheatsheet-editors.ps1 remove <username>
#   .\manage-cheatsheet-editors.ps1 list
#   .\manage-cheatsheet-editors.ps1 list -ServiceAccountPath "path\to\sa.json"
#
# Examples:
#   .\manage-cheatsheet-editors.ps1 add jdoe
#   .\manage-cheatsheet-editors.ps1 remove jdoe
#   .\manage-cheatsheet-editors.ps1 list

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("add", "remove", "list")]
    [string]$Action,

    [Parameter(Mandatory=$false, Position=1)]
    [string]$Username,

    [Parameter(Mandatory=$false)]
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

try {
    $script:accessToken = Get-ServiceAccountAccessToken $ServiceAccountPath
} catch {
    Write-Host "Error: Failed to get access token: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

function Get-FirebaseUrl([string]$path) {
    return "$baseUrl/$path.json?access_token=$($script:accessToken)"
}

switch ($Action) {
    "add" {
        if ([string]::IsNullOrWhiteSpace($Username)) {
            Write-Host "Error: Username is required." -ForegroundColor Red
            Write-Host "Usage: .\manage-cheatsheet-editors.ps1 add <username>" -ForegroundColor Yellow
            exit 1
        }

        $normalized = $Username.ToLower()
        Write-Host "Granting cheat sheet editor role to '$normalized'..." -ForegroundColor Cyan

        try {
            $url = Get-FirebaseUrl "cheat_sheet_editors/$normalized"
            Invoke-RestMethod -Uri $url -Method Put -Body "true" -ContentType "application/json" | Out-Null
            Write-Host "Done! '$normalized' is now a cheat sheet editor." -ForegroundColor Green
            Write-Host "The user will see Edit/Add buttons in the Cheat Sheet widget." -ForegroundColor Gray
        } catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "Check service account permissions." -ForegroundColor Yellow
        }
    }

    "remove" {
        if ([string]::IsNullOrWhiteSpace($Username)) {
            Write-Host "Error: Username is required." -ForegroundColor Red
            Write-Host "Usage: .\manage-cheatsheet-editors.ps1 remove <username>" -ForegroundColor Yellow
            exit 1
        }

        $normalized = $Username.ToLower()
        Write-Host "Revoking cheat sheet editor role from '$normalized'..." -ForegroundColor Cyan

        try {
            $url = Get-FirebaseUrl "cheat_sheet_editors/$normalized"
            Invoke-RestMethod -Uri $url -Method Delete -ContentType "application/json" | Out-Null
            Write-Host "Done! '$normalized' is no longer a cheat sheet editor." -ForegroundColor Green
            Write-Host "Note: If they are an admin, they still have edit access." -ForegroundColor Gray
        } catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    "list" {
        Write-Host "Fetching cheat sheet editors..." -ForegroundColor Cyan

        try {
            $url = Get-FirebaseUrl "cheat_sheet_editors"
            $editors = Invoke-RestMethod -Uri $url -Method Get -ContentType "application/json"

            if ($null -eq $editors -or $editors -eq "null") {
                Write-Host "No cheat sheet editors found." -ForegroundColor Yellow
            } else {
                Write-Host ""
                Write-Host "Cheat Sheet Editors:" -ForegroundColor Green
                Write-Host "--------------------" -ForegroundColor Green

                $count = 0
                foreach ($prop in $editors.PSObject.Properties) {
                    $status = if ($prop.Value -eq $true -or $prop.Value -eq "True") { "active" } else { "inactive" }
                    $color = if ($status -eq "active") { "Green" } else { "DarkGray" }
                    Write-Host "  $($prop.Name) [$status]" -ForegroundColor $color
                    $count++
                }

                if ($count -eq 0) {
                    Write-Host "  (none)" -ForegroundColor DarkGray
                }
                Write-Host ""
                Write-Host "Total: $count editor(s)" -ForegroundColor Cyan
            }

            # Also show admins (who automatically have editor access)
            Write-Host ""
            Write-Host "Admins (automatic editor access):" -ForegroundColor Cyan

            $urlAdmin = Get-FirebaseUrl "admin_users"
            $admins = Invoke-RestMethod -Uri $urlAdmin -Method Get -ContentType "application/json"

            if ($null -eq $admins -or $admins -eq "null") {
                Write-Host "  (none)" -ForegroundColor DarkGray
            } else {
                foreach ($prop in $admins.PSObject.Properties) {
                    if ($prop.Value -eq $true -or $prop.Value -eq "True") {
                        Write-Host "  $($prop.Name) [admin]" -ForegroundColor Magenta
                    }
                }
            }
        } catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}
