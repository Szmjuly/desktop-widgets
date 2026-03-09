#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Simple Firebase version updater (no authentication required for development)
.DESCRIPTION
    Updates DesktopHub version in Firebase using REST API.
    Note: This uses unauthenticated access - update Firebase rules to allow writes to app_versions for development.
.EXAMPLE
    .\Update-FirebaseVersion-Simple.ps1 -Version "1.0.1" -ReleaseNotes "Bug fixes" -DownloadUrl "https://github.com/user/repo/releases"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = "New version available",
    
    [Parameter(Mandatory=$false)]
    [string]$DownloadUrl = "",
    
    [Parameter(Mandatory=$false)]
    [bool]$RequiredUpdate = $false
)

$ErrorActionPreference = "Stop"

$AppId = "desktophub"
$DatabaseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com"

Write-Host "`n🔥 Firebase Version Updater" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Cyan

# Prepare data
$versionData = @{
    latest_version = $Version
    release_date = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    release_notes = $ReleaseNotes
    download_url = $DownloadUrl
    required_update = $RequiredUpdate
    updated_at = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}

Write-Host "`n📦 Update Details:"
Write-Host "  App ID:         $AppId"
Write-Host "  Version:        $Version"
Write-Host "  Release Notes:  $ReleaseNotes"
Write-Host "  Download URL:   $DownloadUrl"
Write-Host "  Required:       $RequiredUpdate"

# Update Firebase
$url = "$DatabaseUrl/app_versions/$AppId.json"
$json = $versionData | ConvertTo-Json -Depth 10

Write-Host "`n🚀 Sending to Firebase..."

try {
    $response = Invoke-RestMethod -Uri $url -Method Put -Body $json -ContentType "application/json"
    
    Write-Host "✅ Success!" -ForegroundColor Green
    Write-Host "`n🌐 View at:" -ForegroundColor Cyan
    Write-Host "   https://console.firebase.google.com/project/licenses-ff136/database/licenses-ff136-default-rtdb/data/app_versions/$AppId"
    Write-Host ""
    
} catch {
    Write-Host "❌ Failed!" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host "`n💡 Tip: Update Firebase rules to allow writes to /app_versions for development:"
    Write-Host '   "app_versions": { ".read": true, ".write": true }'
    exit 1
}
