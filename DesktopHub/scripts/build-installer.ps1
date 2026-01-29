# Build DesktopHub and create installer
param(
    [switch]$SkipBuild,
    [switch]$IncludeAutoStart
)

Write-Host "DesktopHub Build and Installer Script" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green

# Build the application first (unless skipped)
if (-not $SkipBuild) {
    Write-Host "Step 1: Building DesktopHub application..." -ForegroundColor Yellow
    & ".\scripts\build-single-file.ps1"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Application build failed" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Step 1: Skipping application build (using existing)" -ForegroundColor Yellow
}

# Check if WiX Toolset is available
$wixPath = Get-Command "candle.exe" -ErrorAction SilentlyContinue
if (-not $wixPath) {
    Write-Host "❌ WiX Toolset not found. Please install WiX Toolset v3.11 or later." -ForegroundColor Red
    Write-Host "Download from: https://wixtoolset.org/releases/" -ForegroundColor Gray
    exit 1
}

Write-Host "Step 2: Creating installer..." -ForegroundColor Yellow

# Create output directory
$installerDir = "installer-output"
if (Test-Path $installerDir) {
    Remove-Item $installerDir -Recurse -Force
}
New-Item -ItemType Directory -Path $installerDir | Out-Null

# Build WiX installer
$wxsFile = "installer\DesktopHub.wxs"
$wixObjFile = "$installerDir\DesktopHub.wixobj"
$wixMsiFile = "$installerDir\DesktopHub.msi"

Write-Host "Compiling WiX source..." -ForegroundColor Gray
candle.exe $wxsFile -out $wixObjFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ WiX compilation failed" -ForegroundColor Red
    exit 1
}

Write-Host "Linking WiX installer..." -ForegroundColor Gray
light.exe $wixObjFile -out $wixMsiFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ WiX linking failed" -ForegroundColor Red
    exit 1
}

# Check if installer was created
if (Test-Path $wixMsiFile) {
    $installerSize = (Get-Item $wixMsiFile).Length / 1MB
    Write-Host "✅ Installer created successfully!" -ForegroundColor Green
    Write-Host "📁 Installer: $wixMsiFile" -ForegroundColor Cyan
    Write-Host "📊 Size: {0:F2} MB" -f $installerSize -ForegroundColor Cyan
    Write-Host ""
    Write-Host "You can now distribute the MSI installer to users." -ForegroundColor White
    Write-Host "The installer will:" -ForegroundColor Gray
    Write-Host "  • Install DesktopHub to Program Files" -ForegroundColor Gray
    Write-Host "  • Create Start Menu shortcut" -ForegroundColor Gray
    Write-Host "  • Create Desktop shortcut" -ForegroundColor Gray
    Write-Host "  • Allow uninstall via Programs & Features" -ForegroundColor Gray
} else {
    Write-Host "❌ Installer creation failed: MSI file not found" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Build complete! 🎉" -ForegroundColor Green
