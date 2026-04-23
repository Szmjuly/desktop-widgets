#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Manage dev role for a DesktopHub tenant. Thin wrapper over admin-cli.js.

.DESCRIPTION
    Shells out to scripts/admin-cli.js. Writes
    tenants/{tenantId}/dev_users/{user_id} and maintains the user_directory.

.EXAMPLE
    .\manage-dev.ps1 add smarkowitz
    .\manage-dev.ps1 remove jdoe
    .\manage-dev.ps1 list
#>
param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("add", "remove", "list")]
    [string] $Action,

    [Parameter(Position=1)]
    [string] $Username,

    [string] $TenantId = "ces",

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

switch ($Action) {
    "add" {
        if (-not $Username) { throw "Username required for 'add'." }
        & node $cli set-dev $Username --tenant $TenantId
    }
    "remove" {
        if (-not $Username) { throw "Username required for 'remove'." }
        & node $cli remove-dev $Username --tenant $TenantId
    }
    "list" {
        & node $cli list --tenant $TenantId
    }
}
exit $LASTEXITCODE
