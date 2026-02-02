#!/usr/bin/env pwsh
# Quick version updater for DesktopHub
# Usage: .\update-version.ps1 "1.0.1" "Bug fixes and improvements"

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Version,
    
    [Parameter(Mandatory=$false, Position=1)]
    [string]$ReleaseNotes = "New version available"
)

$url = "https://licenses-ff136-default-rtdb.firebaseio.com/app_versions/desktophub.json"

$data = @{
    latest_version = $Version
    release_date = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    release_notes = $ReleaseNotes
    download_url = "https://github.com/yourusername/desktophub/releases/tag/v$Version"
    required_update = $false
    updated_at = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
} | ConvertTo-Json

Write-Host "🚀 Updating DesktopHub to v$Version..." -ForegroundColor Cyan

try {
    Invoke-RestMethod -Uri $url -Method Put -Body $data -ContentType "application/json" | Out-Null
    Write-Host "✅ Success! Version $Version is now live." -ForegroundColor Green
    Write-Host "🌐 View: https://console.firebase.google.com/project/licenses-ff136/database" -ForegroundColor Gray
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "💡 Fix: Set Firebase rules to allow writes to /app_versions" -ForegroundColor Yellow
}
