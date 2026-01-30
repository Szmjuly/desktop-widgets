# Simple DesktopHub Installation Script
param(
    [string]$InstallPath = "$env:ProgramFiles\DesktopHub",
    [switch]$CreateDesktopShortcut,
    [switch]$AutoStart
)

Write-Host "DesktopHub Installation Script" -ForegroundColor Green
Write-Host "=============================" -ForegroundColor Green

# Get source executable path
$projectRoot = Split-Path $PSScriptRoot -Parent
$sourceExe = Join-Path $projectRoot "publish\DesktopHub.exe"

if (-not (Test-Path $sourceExe)) {
    Write-Host "❌ DesktopHub.exe not found at: $sourceExe" -ForegroundColor Red
    Write-Host "Please build the application first: .\build-single-file.ps1" -ForegroundColor Yellow
    exit 1
}

# Create installation directory
Write-Host "Creating installation directory..." -ForegroundColor Yellow
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Copy executable
Write-Host "Copying DesktopHub.exe to: $InstallPath" -ForegroundColor Yellow
Copy-Item $sourceExe $InstallPath -Force
$installedExe = Join-Path $InstallPath "DesktopHub.exe"

# Create Start Menu shortcut
Write-Host "Creating Start Menu shortcut..." -ForegroundColor Yellow
$startMenuPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\DesktopHub"
if (-not (Test-Path $startMenuPath)) {
    New-Item -ItemType Directory -Path $startMenuPath -Force | Out-Null
}

$shortcutPath = Join-Path $startMenuPath "DesktopHub.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $installedExe
$shortcut.WorkingDirectory = $InstallPath
$shortcut.Description = "DesktopHub - Project Search and Widget Launcher"
$shortcut.Save()

Write-Host "✅ Start Menu shortcut created - DesktopHub is now searchable!" -ForegroundColor Green

# Create desktop shortcut if requested
if ($CreateDesktopShortcut) {
    Write-Host "Creating Desktop shortcut..." -ForegroundColor Yellow
    $desktopShortcut = Join-Path $env:USERPROFILE "Desktop\DesktopHub.lnk"
    $shortcut = $shell.CreateShortcut($desktopShortcut)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.Description = "DesktopHub - Project Search and Widget Launcher"
    $shortcut.Save()
    Write-Host "✅ Desktop shortcut created" -ForegroundColor Green
}

# Add to startup if requested
if ($AutoStart) {
    Write-Host "Adding to startup..." -ForegroundColor Yellow
    $startupPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup"
    $startupShortcut = Join-Path $startupPath "DesktopHub.lnk"
    $shortcut = $shell.CreateShortcut($startupShortcut)
    $shortcut.TargetPath = $installedExe
    $shortcut.WorkingDirectory = $InstallPath
    $shortcut.Description = "DesktopHub - Project Search and Widget Launcher"
    $shortcut.Save()
    Write-Host "✅ Added to startup" -ForegroundColor Green
}

# Create simple uninstall script
Write-Host "Creating uninstall script..." -ForegroundColor Yellow
$uninstallScript = @"
# DesktopHub Uninstall Script
Write-Host "Uninstalling DesktopHub..." -ForegroundColor Yellow

# Remove installation directory
if (Test-Path '$InstallPath') {
    Remove-Item '$InstallPath' -Recurse -Force
    Write-Host "✅ Removed installation directory" -ForegroundColor Green
}

# Remove Start Menu shortcut
`$startMenuShortcut = "`$env:APPDATA\Microsoft\Windows\Start Menu\Programs\DesktopHub\DesktopHub.lnk"
if (Test-Path `$startMenuShortcut) {
    Remove-Item `$startMenuShortcut -Force
    Write-Host "✅ Removed Start Menu shortcut" -ForegroundColor Green
}

# Remove desktop shortcut if exists
`$desktopShortcut = "`$env:USERPROFILE\Desktop\DesktopHub.lnk"
if (Test-Path `$desktopShortcut) {
    Remove-Item `$desktopShortcut -Force
    Write-Host "✅ Removed desktop shortcut" -ForegroundColor Green
}

# Remove from startup
`$startupShortcut = "`$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup\DesktopHub.lnk"
if (Test-Path `$startupShortcut) {
    Remove-Item `$startupShortcut -Force
    Write-Host "✅ Removed from startup" -ForegroundColor Green
}

Write-Host "DesktopHub uninstalled successfully!" -ForegroundColor Green
"@

$uninstallScriptPath = Join-Path $InstallPath "uninstall.ps1"
$uninstallScript | Out-File -FilePath $uninstallScriptPath -Encoding UTF8

# Installation summary
Write-Host ""
Write-Host "🎉 DesktopHub installed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Installation details:" -ForegroundColor Cyan
Write-Host "  📁 Location: $installedExe" -ForegroundColor Gray
Write-Host "  🏢 Start Menu: Available in Start Menu" -ForegroundColor Gray
Write-Host "  🔍 Search: Type 'DesktopHub' in Windows Search" -ForegroundColor Gray

if ($CreateDesktopShortcut) {
    Write-Host "  🖥️  Desktop: Desktop shortcut created" -ForegroundColor Gray
}

if ($AutoStart) {
    Write-Host "  🚀 Startup: Will start with Windows" -ForegroundColor Gray
}

Write-Host ""
Write-Host "To uninstall, run: $uninstallScriptPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "You can now:" -ForegroundColor White
Write-Host "  • Press Windows key and search for 'DesktopHub'" -ForegroundColor Gray
Write-Host "  • Find it in Start Menu under DesktopHub folder" -ForegroundColor Gray
Write-Host "  • Run: $installedExe" -ForegroundColor Gray
