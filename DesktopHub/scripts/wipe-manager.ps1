#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Granular DB wipe for the multi-tenant layout. Thin shim over admin-cli.js.

.DESCRIPTION
    The post-refactor schema puts every user-scoped record under tenants/{id}.
    This script routes a requested wipe mode into admin-cli.js (which owns
    firebase-admin auth and DB access). Two-step: first call prints what
    would be deleted, second call with -Force actually deletes.

    Modes:
      all             every top-level node incl. every tenant
      non-tenant      every top-level node except /tenants
      tenant          /tenants/{Tenant}
      tenant-section  /tenants/{Tenant}/{Section}

    Sections: admin_users, dev_users, cheat_sheet_editors, users, devices,
              metrics, events, errors, licenses

.EXAMPLE
    .\wipe-manager.ps1 -Mode non-tenant
    .\wipe-manager.ps1 -Mode non-tenant -Force
    .\wipe-manager.ps1 -Mode tenant -Tenant internal -Force
    .\wipe-manager.ps1 -Mode tenant-section -Tenant ces -Section devices -Force
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet("all", "non-tenant", "tenant", "tenant-section")]
    [string] $Mode,

    [string] $Tenant = "ces",

    [ValidateSet("admin_users", "dev_users", "cheat_sheet_editors",
                 "users", "devices", "metrics", "events", "errors", "licenses")]
    [string] $Section,

    [switch] $Force,

    # Passed through by admin.ps1; sets FIREBASE_ADMIN_KEY_PATH for admin-cli.js.
    [string] $ServiceAccountPath
)

$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
$cli = Join-Path $scriptDir "admin-cli.js"
if (-not (Test-Path $cli)) { throw "admin-cli.js not found at $cli" }

$env:NODE_PATH = Join-Path $scriptDir "..\functions\node_modules"
if ($ServiceAccountPath -and (Test-Path $ServiceAccountPath)) {
    $env:FIREBASE_ADMIN_KEY_PATH = (Resolve-Path $ServiceAccountPath).Path
}
if (-not $env:FIREBASE_ADMIN_KEY_PATH) {
    # Try the canonical admin.ps1 locations as a last resort.
    foreach ($c in @(
        (Join-Path $scriptDir "..\secrets\firebase-license.json"),
        (Join-Path $HOME ".desktophub\firebase-admin-key.json")
    )) {
        if (Test-Path $c) { $env:FIREBASE_ADMIN_KEY_PATH = (Resolve-Path $c).Path; break }
    }
}
if (-not $env:FIREBASE_ADMIN_KEY_PATH) {
    throw "No service account key. Pass -ServiceAccountPath or set FIREBASE_ADMIN_KEY_PATH."
}

# ─── confirmation phrases (admin-cli.js checks --yes <phrase> matches) ───
$confirmPhrase = switch ($Mode) {
    "all"            { "NUKE" }
    "non-tenant"     { "WIPE" }
    "tenant"         { $Tenant }
    "tenant-section" { "WIPE" }
}

# Build arg array for admin-cli.js
$cmd = switch ($Mode) {
    "all"            { "wipe-all" }
    "non-tenant"     { "wipe-non-tenant" }
    "tenant"         { "wipe-tenant" }
    "tenant-section" {
        if (-not $Section) { throw "Mode 'tenant-section' requires -Section." }
        "wipe-section"
    }
}

$args = @($cmd, "--tenant", $Tenant)
if ($Mode -eq "tenant-section") { $args += @("--section", $Section) }
if ($Force)                     { $args += @("--yes", $confirmPhrase) }

# Dry-run first pass so the user sees what's targeted; real commit needs -Force.
Write-Host "Running: node admin-cli.js $($args -join ' ')" -ForegroundColor Cyan
& node $cli @args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not $Force) {
    Write-Host ""
    Write-Host "Rerun with -Force to commit the deletion." -ForegroundColor Yellow
}
