#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates DesktopHub version information in Firebase
.DESCRIPTION
    This script updates the app_versions node in Firebase with new version info.
    It reads the service account key from secrets/firebase-license.json and uses
    the Firebase REST API to update the version information.
.PARAMETER Version
    The new version number (e.g., "1.0.1")
.PARAMETER ReleaseNotes
    Release notes for this version
.PARAMETER DownloadUrl
    URL where users can download the new version
.PARAMETER RequiredUpdate
    Whether this is a required update (default: false)
.EXAMPLE
    .\Update-FirebaseVersion.ps1 -Version "1.0.1" -ReleaseNotes "Bug fixes" -DownloadUrl "https://github.com/user/repo/releases/v1.0.1"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$ReleaseNotes = "",
    
    [Parameter(Mandatory=$false)]
    [string]$DownloadUrl = "",
    
    [Parameter(Mandatory=$false)]
    [bool]$RequiredUpdate = $false
)

$ErrorActionPreference = "Stop"

# Configuration
$AppId = "desktophub"
$DatabaseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com"
$ServiceAccountPath = Join-Path $PSScriptRoot "..\secrets\firebase-license.json"

Write-Host "🔥 Firebase Version Updater" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Check if service account file exists
if (-not (Test-Path $ServiceAccountPath)) {
    Write-Error "Service account file not found at: $ServiceAccountPath"
    exit 1
}

Write-Host "📄 Loading service account credentials..." -ForegroundColor Yellow
$serviceAccount = Get-Content $ServiceAccountPath -Raw | ConvertFrom-Json

# Get OAuth2 access token
Write-Host "🔐 Getting access token..." -ForegroundColor Yellow

$jwtHeader = @{
    alg = "RS256"
    typ = "JWT"
} | ConvertTo-Json -Compress

$now = [int][double]::Parse((Get-Date -UFormat %s))
$expiry = $now + 3600

$jwtPayload = @{
    iss = $serviceAccount.client_email
    scope = "https://www.googleapis.com/auth/userinfo.email https://www.googleapis.com/auth/firebase.database"
    aud = "https://oauth2.googleapis.com/token"
    exp = $expiry
    iat = $now
} | ConvertTo-Json -Compress

# Use gcloud to get token (simpler than implementing JWT signing in PowerShell)
try {
    # Try using gcloud if available
    $token = (gcloud auth application-default print-access-token 2>$null)
    if (-not $token) {
        throw "gcloud not available"
    }
} catch {
    Write-Host "⚠️  gcloud not found. Attempting alternative method..." -ForegroundColor Yellow
    
    # Alternative: Use the Firebase REST API with database secret (if available)
    # Or use a pre-generated token
    Write-Warning "Please install Google Cloud SDK for automatic authentication, or provide an access token."
    Write-Warning "Install gcloud: https://cloud.google.com/sdk/docs/install"
    
    $manualToken = Read-Host "Enter a Firebase access token (or press Enter to try without auth)"
    if ($manualToken) {
        $token = $manualToken
    } else {
        Write-Warning "Proceeding without authentication - this may fail if security rules require auth"
        $token = $null
    }
}

# Prepare version data
$versionData = @{
    latest_version = $Version
    release_date = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    updated_at = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}

if ($ReleaseNotes) {
    $versionData.release_notes = $ReleaseNotes
}

if ($DownloadUrl) {
    $versionData.download_url = $DownloadUrl
}

$versionData.required_update = $RequiredUpdate

# Build Firebase URL
$firebaseUrl = "$DatabaseUrl/app_versions/$AppId.json"
if ($token) {
    $firebaseUrl += "?access_token=$token"
}

Write-Host ""
Write-Host "📦 Version Information:" -ForegroundColor Cyan
Write-Host "  Version:        $Version"
Write-Host "  Release Notes:  $ReleaseNotes"
Write-Host "  Download URL:   $DownloadUrl"
Write-Host "  Required:       $RequiredUpdate"
Write-Host ""

# Update Firebase
Write-Host "🚀 Updating Firebase..." -ForegroundColor Yellow

try {
    $jsonBody = $versionData | ConvertTo-Json -Depth 10
    
    $response = Invoke-RestMethod -Uri $firebaseUrl -Method Put -Body $jsonBody -ContentType "application/json"
    
    Write-Host "✅ Successfully updated version in Firebase!" -ForegroundColor Green
    Write-Host ""
    Write-Host "🌐 View in console: https://console.firebase.google.com/project/licenses-ff136/database/licenses-ff136-default-rtdb/data/app_versions/$AppId" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Response:" -ForegroundColor Gray
    Write-Host ($response | ConvertTo-Json -Depth 10) -ForegroundColor Gray
    
} catch {
    Write-Error "Failed to update Firebase: $_"
    Write-Error $_.Exception.Message
    exit 1
}
