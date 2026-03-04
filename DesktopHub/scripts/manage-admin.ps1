#!/usr/bin/env pwsh
# Admin management for DesktopHub Metrics Viewer
# Manages admin_users in Firebase Realtime Database by Windows username.
#
# Usage:
#   .\manage-admin.ps1 add <username>       # Grant admin privileges
#   .\manage-admin.ps1 remove <username>    # Revoke admin privileges
#   .\manage-admin.ps1 list                 # List all admin users
#
# Examples:
#   .\manage-admin.ps1 add smarkowitz
#   .\manage-admin.ps1 remove jdoe
#   .\manage-admin.ps1 list

param(
    [Parameter(Mandatory=$true, Position=0)]
    [ValidateSet("add", "remove", "list")]
    [string]$Action,

    [Parameter(Mandatory=$false, Position=1)]
    [string]$Username
)

$baseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com"

function Get-AdminUsers {
    try {
        $response = Invoke-RestMethod -Uri "$baseUrl/admin_users.json" -Method Get -ContentType "application/json"
        return $response
    } catch {
        Write-Host "Error fetching admin users: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

switch ($Action) {
    "add" {
        if ([string]::IsNullOrWhiteSpace($Username)) {
            Write-Host "Error: Username is required for 'add' action." -ForegroundColor Red
            Write-Host "Usage: .\manage-admin.ps1 add <username>" -ForegroundColor Yellow
            exit 1
        }

        $normalizedUsername = $Username.ToLower()
        Write-Host "Granting admin privileges to '$normalizedUsername'..." -ForegroundColor Cyan

        try {
            $url = "$baseUrl/admin_users/$normalizedUsername.json"
            Invoke-RestMethod -Uri $url -Method Put -Body '"true"' -ContentType "application/json" | Out-Null
            Write-Host "Admin granted to '$normalizedUsername'." -ForegroundColor Green
        } catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    "remove" {
        if ([string]::IsNullOrWhiteSpace($Username)) {
            Write-Host "Error: Username is required for 'remove' action." -ForegroundColor Red
            Write-Host "Usage: .\manage-admin.ps1 remove <username>" -ForegroundColor Yellow
            exit 1
        }

        $normalizedUsername = $Username.ToLower()
        Write-Host "Revoking admin privileges from '$normalizedUsername'..." -ForegroundColor Cyan

        try {
            $url = "$baseUrl/admin_users/$normalizedUsername.json"
            Invoke-RestMethod -Uri $url -Method Delete -ContentType "application/json" | Out-Null
            Write-Host "Admin revoked from '$normalizedUsername'." -ForegroundColor Green
        } catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    "list" {
        Write-Host "Fetching admin users..." -ForegroundColor Cyan

        $admins = Get-AdminUsers
        if ($null -eq $admins) {
            Write-Host "No admin users found (or unable to fetch)." -ForegroundColor Yellow
        } else {
            Write-Host ""
            Write-Host "Admin Users:" -ForegroundColor Green
            Write-Host "------------" -ForegroundColor Green

            $adminProps = $admins.PSObject.Properties
            $count = 0
            foreach ($prop in $adminProps) {
                $status = if ($prop.Value -eq "true" -or $prop.Value -eq $true) { "active" } else { "inactive" }
                $color = if ($status -eq "active") { "Green" } else { "DarkGray" }
                Write-Host "  $($prop.Name) [$status]" -ForegroundColor $color
                $count++
            }

            if ($count -eq 0) {
                Write-Host "  (none)" -ForegroundColor DarkGray
            }
            Write-Host ""
            Write-Host "Total: $count admin user(s)" -ForegroundColor Cyan
        }
    }
}
