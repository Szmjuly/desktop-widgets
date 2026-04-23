#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Manage cheat-sheet editor role for a DesktopHub tenant. Wrapper over admin-cli.js.
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
        & node $cli set-editor $Username --tenant $TenantId
    }
    "remove" {
        if (-not $Username) { throw "Username required for 'remove'." }
        & node $cli remove-editor $Username --tenant $TenantId
    }
    "list" {
        & node $cli list --tenant $TenantId
    }
}
exit $LASTEXITCODE
