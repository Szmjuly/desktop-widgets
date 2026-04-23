#!/usr/bin/env pwsh
# Firebase Authentication cleanup tool
# Deletes Firebase Auth users in bulk using a service account (admin privileges).
#
# Usage:
#   .\cleanup-auth-users.ps1                         # Preview only (no delete)
#   .\cleanup-auth-users.ps1 -DeleteAll              # Delete all auth users after confirmation
#   .\cleanup-auth-users.ps1 -DeleteAll -AnonymousOnly
#   .\cleanup-auth-users.ps1 -DeleteAll -Force       # Skip confirmation prompt
#   .\cleanup-auth-users.ps1 -ServiceAccountPath "path\to\sa.json"

param(
    [switch]$DeleteAll,
    [switch]$AnonymousOnly,
    [switch]$Force,
    [string]$ServiceAccountPath
)

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
    # Use the modern cloud-platform scope. The legacy "identitytoolkit"
    # OAuth scope was deprecated in 2020; Google now returns HTTP 400
    # during token exchange when it's requested. cloud-platform is the
    # superset that includes the Identity Toolkit admin APIs this script
    # hits (projects:accounts:batchGet / batchDelete).
    $claims = @{
        iss   = $email
        scope = "https://www.googleapis.com/auth/cloud-platform"
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

    try {
        $response = Invoke-RestMethod -Uri "https://oauth2.googleapis.com/token" -Method Post -Body @{
            grant_type = "urn:ietf:params:oauth:grant-type:jwt-bearer"
            assertion  = $jwt
        } -ContentType "application/x-www-form-urlencoded" -TimeoutSec 20
    } catch [System.Net.WebException] {
        # Google's OAuth endpoint returns a JSON body like
        # {"error":"invalid_grant","error_description":"..."} on 4xx.
        # Surface that so we can diagnose instead of just "400 Bad Request".
        $resp = $_.Exception.Response
        $body = "(no body)"
        if ($null -ne $resp) {
            try {
                $stream = $resp.GetResponseStream()
                $reader = [System.IO.StreamReader]::new($stream)
                $body = $reader.ReadToEnd()
            } catch {}
        }
        throw "OAuth token exchange failed: $($_.Exception.Message)`nGoogle said: $body`nService account: $email`nKey ID prefix: $($sa.private_key_id.Substring(0, [Math]::Min(12, $sa.private_key_id.Length)))..."
    }

    return @{
        AccessToken = $response.access_token
        ProjectId   = $sa.project_id
    }
}

# ============================================================
# SERVICE ACCOUNT DISCOVERY
# ============================================================

if ([string]::IsNullOrWhiteSpace($ServiceAccountPath)) {
    $scriptDir = $PSScriptRoot
    # Prefer the off-repo per-user secret store, falling back to legacy paths.
    # The Renamer path was removed on 2026-04-22 after the credential rotation.
    $candidates = @(
        $env:FIREBASE_ADMIN_KEY_PATH,
        (Join-Path $env:USERPROFILE ".desktophub\firebase-admin-key.json"),
        (Join-Path $scriptDir "..\secrets\firebase-license.json")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($c in $candidates) {
        if (Test-Path $c) {
            $ServiceAccountPath = (Resolve-Path $c).Path
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ServiceAccountPath) -or -not (Test-Path $ServiceAccountPath)) {
    Write-Host "Error: Service account JSON not found." -ForegroundColor Red
    Write-Host "Looked in (in priority order):" -ForegroundColor Yellow
    Write-Host "  `$env:FIREBASE_ADMIN_KEY_PATH = '$env:FIREBASE_ADMIN_KEY_PATH'" -ForegroundColor Yellow
    Write-Host "  $env:USERPROFILE\.desktophub\firebase-admin-key.json" -ForegroundColor Yellow
    Write-Host "  DesktopHub\secrets\firebase-license.json" -ForegroundColor Yellow
    Write-Host "Or provide: -ServiceAccountPath <path>" -ForegroundColor Yellow
    exit 1
}

# Diagnostic: which key is being used
try {
    $debugSa = Get-Content $ServiceAccountPath -Raw | ConvertFrom-Json
    Write-Host ("Using key: {0}" -f $ServiceAccountPath) -ForegroundColor DarkGray
    Write-Host ("  Service account: {0}" -f $debugSa.client_email) -ForegroundColor DarkGray
    Write-Host ("  Key ID prefix:   {0}..." -f $debugSa.private_key_id.Substring(0, 12)) -ForegroundColor DarkGray
} catch {
    Write-Host "Warning: could not inspect key file for diagnostic info" -ForegroundColor DarkGray
}

Write-Host "Authenticating with service account..." -ForegroundColor Gray
try {
    $auth = Get-ServiceAccountAccessToken $ServiceAccountPath
    $accessToken = $auth.AccessToken
    $projectId = $auth.ProjectId
    if ([string]::IsNullOrWhiteSpace($projectId)) {
        throw "project_id missing in service account JSON"
    }
    Write-Host "Authenticated. Project: $projectId" -ForegroundColor Green
} catch {
    Write-Host "Error: Failed to authenticate: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $accessToken" }

function Get-AllAuthUsers {
    param(
        [string]$ProjectId,
        [hashtable]$Headers
    )

    $allUsers = @()
    $nextPageToken = $null

    do {
        $url = "https://identitytoolkit.googleapis.com/v1/projects/${ProjectId}/accounts:batchGet?maxResults=1000"
        if (-not [string]::IsNullOrWhiteSpace($nextPageToken)) {
            $url += "&nextPageToken=$([System.Uri]::EscapeDataString($nextPageToken))"
        }

        try {
            $resp = Invoke-RestMethod -Uri $url -Method Get -Headers $Headers -TimeoutSec 30
        } catch {
            # Capture detailed error from Google API
            $errBody = ""
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $errBody = $reader.ReadToEnd()
                $reader.Close()
            } catch {}
            throw "HTTP $($_.Exception.Response.StatusCode): $errBody"
        }

        if ($resp.users) {
            $allUsers += @($resp.users)
        }

        $nextPageToken = $resp.nextPageToken
    } while (-not [string]::IsNullOrWhiteSpace($nextPageToken))

    return $allUsers
}

function Remove-AuthUsersBatch {
    param(
        [string]$ProjectId,
        [hashtable]$Headers,
        [array]$LocalIds
    )

    $url = "https://identitytoolkit.googleapis.com/v1/projects/$ProjectId/accounts:batchDelete"
    $body = @{ localIds = $LocalIds; force = $true } | ConvertTo-Json -Depth 5
    Invoke-RestMethod -Uri $url -Method Post -Headers $Headers -Body $body -ContentType "application/json" -TimeoutSec 30 | Out-Null
}

try {
    $users = Get-AllAuthUsers -ProjectId $projectId -Headers $headers
} catch {
    Write-Host "Error: Failed to list auth users: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

if (-not $users -or $users.Count -eq 0) {
    Write-Host "No Firebase Auth users found." -ForegroundColor Yellow
    exit 0
}

$anonymous = @($users | Where-Object { -not $_.email -and (-not $_.providerUserInfo -or $_.providerUserInfo.Count -eq 0) })
$nonAnonymous = $users.Count - $anonymous.Count

Write-Host "" 
Write-Host "Firebase Auth user summary:" -ForegroundColor Cyan
Write-Host "  Total users:      $($users.Count)" -ForegroundColor White
Write-Host "  Anonymous users:  $($anonymous.Count)" -ForegroundColor White
Write-Host "  Non-anonymous:    $nonAnonymous" -ForegroundColor White
Write-Host ""

$targetUsers = if ($AnonymousOnly) { $anonymous } else { $users }
if (-not $targetUsers -or $targetUsers.Count -eq 0) {
    Write-Host "No matching users to delete for the selected filter." -ForegroundColor Yellow
    exit 0
}

Write-Host "Users selected for deletion: $($targetUsers.Count)" -ForegroundColor Yellow
if ($AnonymousOnly) {
    Write-Host "Filter: Anonymous only" -ForegroundColor Gray
} else {
    Write-Host "Filter: All auth users" -ForegroundColor Gray
}

if (-not $DeleteAll) {
    Write-Host "" 
    Write-Host "Preview mode only. Add -DeleteAll to actually delete users." -ForegroundColor Yellow
    exit 0
}

if (-not $Force) {
    Write-Host ""
    $confirm = Read-Host "Type DELETE to permanently remove $($targetUsers.Count) user(s)"
    if ($confirm -ne "DELETE") {
        Write-Host "Cancelled." -ForegroundColor Gray
        exit 0
    }
}

$ids = @($targetUsers | ForEach-Object { $_.localId } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($ids.Count -eq 0) {
    Write-Host "No valid localIds found to delete." -ForegroundColor Yellow
    exit 0
}

Write-Host "" 
Write-Host "Deleting users in batches..." -ForegroundColor Cyan
$batchSize = 1000
$deleted = 0

for ($i = 0; $i -lt $ids.Count; $i += $batchSize) {
    $end = [Math]::Min($i + $batchSize - 1, $ids.Count - 1)
    $chunk = $ids[$i..$end]

    try {
        Remove-AuthUsersBatch -ProjectId $projectId -Headers $headers -LocalIds $chunk
        $deleted += $chunk.Count
        Write-Host "  Deleted batch: $($chunk.Count) (total: $deleted/$($ids.Count))" -ForegroundColor Green
    } catch {
        Write-Host "  Failed batch starting at index ${i}: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Done. Requested delete count: $($ids.Count), deleted: $deleted" -ForegroundColor Green
