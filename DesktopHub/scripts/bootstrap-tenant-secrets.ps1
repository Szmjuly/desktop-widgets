#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Create / rotate per-tenant Secret Manager secrets backing the HMAC user_id
    + encrypted username directory system. Uses the Firebase CLI, not gcloud.

.DESCRIPTION
    Writes two secrets to Secret Manager via `firebase functions:secrets:set`:
      - tenant-salt-{tenantId}      : 32 random bytes (HMAC-SHA256 key)
      - tenant-encrypt-{tenantId}   : base64(32 random bytes) (AES-256-GCM key)

    The Firebase CLI binds access to the runtime service account on the next
    `firebase deploy --only functions` automatically, so there's no IAM step
    to do here.

    WARNING: rotating tenant-salt-{tenantId} invalidates every stored user_id
    for that tenant. Re-enrollment required. The encrypt key rotates safely
    -- next login re-encrypts the directory entry under the new version.

.PARAMETER TenantId
    Tenant identifier, e.g. 'internal', 'acme'. Lowercase, [a-z0-9-]+.

.PARAMETER Project
    Firebase project id. Defaults to the CLI's active project.

.PARAMETER RotateSalt
    Rotate the HMAC salt (destroys all existing user_id hashes).

.EXAMPLE
    ./bootstrap-tenant-secrets.ps1 -TenantId internal
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9\-]+$')]
    [string] $TenantId,

    [string] $Project,

    [switch] $RotateSalt
)

$ErrorActionPreference = 'Stop'

# DPAPI lives in System.Security assembly on PS 5.1 -- not auto-loaded.
Add-Type -AssemblyName System.Security

function Test-Cli {
    param([string] $Name)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $cmd) { throw "'$Name' not on PATH" }
}

Test-Cli -Name firebase
Test-Cli -Name node

function New-RandomBytes {
    param([int] $Count)
    $buf = New-Object byte[] $Count
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buf)
    return ,$buf
}

function Set-FirebaseSecret {
    param(
        [string] $Name,
        [byte[]] $Bytes,
        [switch] $Base64
    )
    $tmp = [System.IO.Path]::GetTempFileName()
    try {
        if ($Base64) {
            # Firebase CLI stores exactly what's on stdin. Write base64 text, no newline.
            [System.IO.File]::WriteAllText($tmp, [Convert]::ToBase64String($Bytes))
        } else {
            [System.IO.File]::WriteAllBytes($tmp, $Bytes)
        }

        $args = @('functions:secrets:set', $Name, "--data-file=$tmp")
        if ($Project) { $args += @('--project', $Project) }

        Write-Host "  -> uploading '$Name' via firebase CLI"
        & firebase @args
        if ($LASTEXITCODE -ne 0) { throw "firebase secrets:set failed for $Name" }
    }
    finally {
        if (Test-Path $tmp) { Remove-Item $tmp -Force }
    }
}

# ─── Cache to local DPAPI-encrypted file so the migration script can read
# ─── the same key values without having to call Secret Manager itself.
$cacheDir = Join-Path $env:LOCALAPPDATA 'DesktopHub\tenant-secrets'
New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
$cachePath = Join-Path $cacheDir "$TenantId.json"

function Save-LocalCache {
    param([byte[]] $Salt, [byte[]] $Encrypt)
    $payload = [PSCustomObject]@{
        tenant_id      = $TenantId
        salt_b64       = [Convert]::ToBase64String($Salt)
        encrypt_key_b64 = [Convert]::ToBase64String($Encrypt)
        saved_at       = (Get-Date).ToString('o')
    } | ConvertTo-Json -Compress

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $protected = [System.Security.Cryptography.ProtectedData]::Protect(
        $bytes, $null,
        [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    [System.IO.File]::WriteAllBytes($cachePath, $protected)
    Write-Host "  -> cached DPAPI-encrypted copy at $cachePath"
}

Write-Host "Bootstrapping tenant secrets for '$TenantId'" -ForegroundColor Cyan
if ($Project) { Write-Host "Project: $Project" }

# Firebase CLI enforces UPPER_SNAKE_CASE secret names.
$tenantUpper = ($TenantId -replace '-', '_').ToUpperInvariant()
$saltName    = "TENANT_SALT_$tenantUpper"
$encryptName = "TENANT_ENCRYPT_$tenantUpper"

if ($RotateSalt) {
    Write-Warning "ROTATING $saltName -- all existing user_id hashes for tenant '$TenantId' will become invalid."
    $confirm = Read-Host "Type the tenant id '$TenantId' to confirm"
    if ($confirm -ne $TenantId) { throw "Aborted." }
}

$saltBytes = New-RandomBytes -Count 32
$encBytes  = New-RandomBytes -Count 32

# Both secrets are uploaded as base64 text. Firebase CLI reads secret files
# as UTF-8 strings; raw binary bytes get mangled by UTF-8 replacement. Storing
# base64 keeps the round-trip lossless. Functions decode base64 before use.
Set-FirebaseSecret -Name $saltName -Bytes $saltBytes -Base64
Set-FirebaseSecret -Name $encryptName -Bytes $encBytes -Base64

Save-LocalCache -Salt $saltBytes -Encrypt $encBytes

Write-Host "`nDone. Tenant '$TenantId' secrets are provisioned." -ForegroundColor Green
Write-Host "Next steps:"
Write-Host "  1. firebase deploy --only functions"
Write-Host "  2. node scripts/migrate-to-tenants.js --tenant $TenantId --dry-run"
