#!/usr/bin/env pwsh
# Admin management for DesktopHub Metrics Viewer
# Manages admin_users in Firebase Realtime Database by Windows username.
#
# Prerequisites: Add this to your Firebase rules (alongside app_versions):
#   "admin_users": { "$username": { ".read": true, ".write": true } }
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

switch ($Action) {
    "add" {
        if ([string]::IsNullOrWhiteSpace($Username)) {
            Write-Host "Error: Username is required." -ForegroundColor Red
            Write-Host "Usage: .\manage-admin.ps1 add <username>" -ForegroundColor Yellow
            exit 1
        }

        $normalized = $Username.ToLower()
        Write-Host "Granting admin to '$normalized'..." -ForegroundColor Cyan

        try {
            $url = "$baseUrl/admin_users/$normalized.json"
            Invoke-RestMethod -Uri $url -Method Put -Body "true" -ContentType "application/json" | Out-Null
            Write-Host "Done! '$normalized' is now an admin." -ForegroundColor Green
            Write-Host "The user will see the admin toggle in Metrics Viewer on next app launch." -ForegroundColor Gray
        } catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "Make sure admin_users rules are added to Firebase." -ForegroundColor Yellow
        }
    }

    "remove" {
        if ([string]::IsNullOrWhiteSpace($Username)) {
            Write-Host "Error: Username is required." -ForegroundColor Red
            Write-Host "Usage: .\manage-admin.ps1 remove <username>" -ForegroundColor Yellow
            exit 1
        }

        $normalized = $Username.ToLower()
        Write-Host "Revoking admin from '$normalized'..." -ForegroundColor Cyan

        try {
            $url = "$baseUrl/admin_users/$normalized.json"
            Invoke-RestMethod -Uri $url -Method Delete -ContentType "application/json" | Out-Null
            Write-Host "Done! '$normalized' is no longer an admin." -ForegroundColor Green
        } catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }

    "list" {
        Write-Host "Fetching admin users..." -ForegroundColor Cyan

        try {
            $url = "$baseUrl/admin_users.json"
            $admins = Invoke-RestMethod -Uri $url -Method Get -ContentType "application/json"

            if ($null -eq $admins -or $admins -eq "null") {
                Write-Host "No admin users found." -ForegroundColor Yellow
            } else {
                Write-Host ""
                Write-Host "Admin Users:" -ForegroundColor Green
                Write-Host "------------" -ForegroundColor Green

                $count = 0
                foreach ($prop in $admins.PSObject.Properties) {
                    $status = if ($prop.Value -eq $true -or $prop.Value -eq "True") { "active" } else { "inactive" }
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
        } catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}
