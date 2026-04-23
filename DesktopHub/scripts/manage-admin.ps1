#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Manage admin role for a DesktopHub tenant. Thin wrapper over admin-cli.js.

.DESCRIPTION
    Shells out to scripts/admin-cli.js (Node) which hashes the username with the
    tenant salt, writes tenants/{tenantId}/admin_users/{user_id}, and maintains
    the encrypted user_directory entry.

    Previously this script wrote directly to a flat admin_users/{username} node.
    Under the multi-tenant rules that path is locked; all role management now
    goes through the tenant-scoped path and the HMAC-hashed user_id.

.PARAMETER Action
    add | remove | list

.PARAMETER Username
    Raw Windows username. Required for add/remove.

.PARAMETER TenantId
    Tenant identifier. Default: internal.

.EXAMPLE
    .\manage-admin.ps1 add smarkowitz
    .\manage-admin.ps1 remove jdoe
    .\manage-admin.ps1 list
#>
param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("add", "remove", "list")]
    [string] $Action,

    [Parameter(Position=1)]
    [string] $Username,

    [string] $TenantId = "ces",

    # Passed through by admin.ps1. admin-cli.js expects FIREBASE_ADMIN_KEY_PATH
    # in the environment; we set it from this parameter if given.
    [string] $ServiceAccountPath
)

$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
$cli = Join-Path $scriptDir "admin-cli.js"
if (-not (Test-Path $cli)) { throw "admin-cli.js not found at $cli" }

# Make firebase-admin resolvable.
$env:NODE_PATH = Join-Path $scriptDir "..\functions\node_modules"
if ($ServiceAccountPath -and (Test-Path $ServiceAccountPath)) {
    $env:FIREBASE_ADMIN_KEY_PATH = (Resolve-Path $ServiceAccountPath).Path
}

switch ($Action) {
    "add" {
        if (-not $Username) { throw "Username required for 'add'." }
        & node $cli set-admin $Username --tenant $TenantId
    }
    "remove" {
        if (-not $Username) { throw "Username required for 'remove'." }
        & node $cli remove-admin $Username --tenant $TenantId
    }
    "list" {
        & node $cli list --tenant $TenantId
    }
}
exit $LASTEXITCODE
