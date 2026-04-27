#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Clean-slate reset for a DesktopHub tenant: rotates secrets, wipes data,
    prompts you through redeploy + client restart + re-granting roles.

.DESCRIPTION
    Use when you want to reset everything about a tenant -- typically during
    testing or after a security event. Walks you through these steps in order:

      1. Rotate the per-tenant HMAC salt + AES encrypt key in Secret Manager.
         Every existing user_id hash becomes invalid (intentional).
      2. Wait for you to run `firebase deploy --only functions` so the new
         secret versions get pinned on the Cloud Functions runtime.
      3. Wipe /tenants/{TenantId}/* entirely. Admin/dev grants, devices,
         metrics, events, errors, licenses, users -- all gone.
      4. Wait for you to close + relaunch the DesktopHub client so it
         signs in fresh against the new salt, producing a new user_id.
      5. Re-grant admin + dev to an operator username of your choice
         (defaults to your current Windows username).

    After this runs, the only data under /tenants/{TenantId}/ is:
      - users/{new_hash}   (auto-populated by the client's first sign-in)
      - devices/{device_id} (written by RegisterUserAndDeviceAsync)
      - admin_users/{new_hash} : true   (written by step 5)
      - dev_users/{new_hash}   : true   (written by step 5)

.PARAMETER TenantId
    Tenant to reset. Default: ces.

.PARAMETER ReGrantUser
    Windows username to grant admin + dev to after reset.
    Default: $env:USERNAME (the operator running this script).

.PARAMETER SkipSecretRotation
    Skip step 1 (secret rotation). Use when you just want to wipe data
    without invalidating user_id hashes -- e.g., clearing stale metrics
    without forcing everyone to re-auth.

.PARAMETER Force
    Skip the final "type the tenant id to confirm" gate. Still pauses
    between steps because redeploy + client restart are manual.

.EXAMPLE
    .\reset-tenant.ps1 -TenantId ces
    Full reset for CES, re-grants admin+dev to $env:USERNAME.

.EXAMPLE
    .\reset-tenant.ps1 -TenantId ces -ReGrantUser aben-habib
    Full reset, re-grants to aben-habib instead of the current operator.

.EXAMPLE
    .\reset-tenant.ps1 -TenantId ces -SkipSecretRotation
    Wipe tenant data but keep current hashes (no secret rotation).
#>
[CmdletBinding()]
param(
    [string] $TenantId = "ces",
    [string] $ReGrantUser = $env:USERNAME,
    [switch] $SkipSecretRotation,
    [switch] $Force,
    [string] $ServiceAccountPath
)

$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot

function Write-Header([string] $Text) {
    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host ("=" * 70) -ForegroundColor Cyan
}

function Pause-For-Manual([string] $Prompt) {
    Write-Host ""
    Write-Host "  $Prompt" -ForegroundColor Yellow
    Read-Host "  Press Enter when done (or Ctrl+C to abort)" | Out-Null
}

# ─── confirm ────────────────────────────────────────────────────────
Write-Header "TENANT RESET: $TenantId"
Write-Host "  This will:"
if (-not $SkipSecretRotation) {
    Write-Host "    1. Rotate TENANT_SALT_$($TenantId.ToUpper()) + TENANT_ENCRYPT_$($TenantId.ToUpper())"
    Write-Host "    2. Wait for you to redeploy Cloud Functions"
    Write-Host "    3. Wipe /tenants/$TenantId/* entirely"
    Write-Host "    4. Wait for you to restart the DesktopHub client"
    Write-Host "    5. Re-grant admin + dev to '$ReGrantUser'"
} else {
    Write-Host "    1. [skipped] Secret rotation"
    Write-Host "    2. Wipe /tenants/$TenantId/* entirely"
    Write-Host "    3. Wait for you to restart the DesktopHub client"
    Write-Host "    4. Re-grant admin + dev to '$ReGrantUser'"
}
Write-Host ""
Write-Host "  All data in the tenant will be deleted." -ForegroundColor Red

if (-not $Force) {
    $confirm = Read-Host "  Type the tenant id '$TenantId' to proceed"
    if ($confirm -ne $TenantId) {
        Write-Host "  Cancelled." -ForegroundColor Gray
        return
    }
}

# ─── 1. Rotate secrets ──────────────────────────────────────────────
if (-not $SkipSecretRotation) {
    Write-Header "Step 1/5: Rotating tenant secrets"
    & (Join-Path $scriptDir "bootstrap-tenant-secrets.ps1") -TenantId $TenantId -RotateSalt
    if ($LASTEXITCODE -ne 0) { throw "Secret rotation failed." }

    # ─── 2. Wait for redeploy ───────────────────────────────────────
    Write-Header "Step 2/5: Redeploy Cloud Functions"
    Write-Host "  Run this in another terminal (or here after pressing Enter):"
    Write-Host "    firebase deploy --only functions" -ForegroundColor Green
    Pause-For-Manual "Redeploy now. The client won't auth correctly until the new secret version is live."
} else {
    Write-Host ""
    Write-Host "  [skipped] Steps 1-2 (secret rotation + redeploy)" -ForegroundColor Gray
}

# ─── 3. Wipe tenant subtree ─────────────────────────────────────────
$wipeStep = if ($SkipSecretRotation) { "Step 2" } else { "Step 3" }
Write-Header "$wipeStep`: Wiping /tenants/$TenantId/*"
$wipeArgs = @{ Mode = "tenant"; Tenant = $TenantId; Force = $true }
if ($ServiceAccountPath) { $wipeArgs['ServiceAccountPath'] = $ServiceAccountPath }
& (Join-Path $scriptDir "wipe-manager.ps1") @wipeArgs
if ($LASTEXITCODE -ne 0) { throw "Tenant wipe failed." }

# ─── 4. Client restart ──────────────────────────────────────────────
$restartStep = if ($SkipSecretRotation) { "Step 3" } else { "Step 4" }
Write-Header "$restartStep`: Restart the DesktopHub client"
Write-Host "  Close DesktopHub (system tray -> Exit) then relaunch:"
Write-Host "    dotnet run --project .\src\DesktopHub.UI -c Debug" -ForegroundColor Green
Write-Host ""
Write-Host "  On restart the client will:"
Write-Host "    - Auto-provision a new FREE license"
Write-Host "    - Sign in under the new salt (new user_id for '$ReGrantUser')"
Write-Host "    - Register its device under /tenants/$TenantId/devices/{deviceId}"
Pause-For-Manual "Restart the client. Wait until the system tray icon is present."

# ─── 5. Re-grant admin + dev ────────────────────────────────────────
$grantStep = if ($SkipSecretRotation) { "Step 4" } else { "Step 5" }
Write-Header "$grantStep`: Re-granting admin + dev to '$ReGrantUser'"
$cli = Join-Path $scriptDir "admin-cli.js"
$env:NODE_PATH = Join-Path $scriptDir "..\functions\node_modules"
if ($ServiceAccountPath -and (Test-Path $ServiceAccountPath)) {
    $env:FIREBASE_ADMIN_KEY_PATH = (Resolve-Path $ServiceAccountPath).Path
}

& node $cli set-admin $ReGrantUser --tenant $TenantId
if ($LASTEXITCODE -ne 0) { throw "set-admin failed." }

& node $cli set-dev $ReGrantUser --tenant $TenantId
if ($LASTEXITCODE -ne 0) { throw "set-dev failed." }

Write-Host ""
Write-Host "  Current tenant users:" -ForegroundColor Cyan
& node $cli list --tenant $TenantId

Write-Header "Reset complete"
Write-Host "  /tenants/$TenantId/admin_users/<new-hash>: true"
Write-Host "  /tenants/$TenantId/dev_users/<new-hash>:   true"
Write-Host ""
Write-Host "  Restart the client ONE MORE TIME so it picks up the new tier=dev claim" -ForegroundColor Yellow
Write-Host "  on its next issueToken call. (Custom token claims are fixed at mint time.)"
