#!/usr/bin/env pwsh
# Build HAPExtractor as a single-file executable
param(
    [string]$Version = "1.0.0"
)

Write-Host "Building HAPExtractor as single-file executable..." -ForegroundColor Green

$projectRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $projectRoot "src\HAPExtractor.UI\HAPExtractor.UI.csproj"
$outputDir = Join-Path $projectRoot "src\HAPExtractor.UI\bin\Release\net8.0-windows\win-x64\publish-v$Version"

# Clean previous build
Write-Host "Cleaning previous build..." -ForegroundColor Yellow
dotnet clean $projectPath -c Release

# Build and publish as single file (pass Version so the exe reports it for update checks)
Write-Host "Building and publishing as single file (Version=$Version)..." -ForegroundColor Yellow
dotnet publish $projectPath -c Release -r win-x64 --self-contained -p:Version=$Version -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -o $outputDir

# Check if build succeeded
if ($LASTEXITCODE -eq 0) {
    $exePath = Join-Path $outputDir "HAPExtractor.exe"
    if (Test-Path $exePath) {
        $fileSize = (Get-Item $exePath).Length / 1MB
        Write-Host "Build successful!" -ForegroundColor Green
        Write-Host "Output: $exePath" -ForegroundColor Cyan
        Write-Host ("Size: {0:F2} MB" -f $fileSize) -ForegroundColor Cyan
        Write-Host ""
        Write-Host "You can now run the executable directly:" -ForegroundColor White
        Write-Host "  $exePath" -ForegroundColor Gray
    } else {
        Write-Host "Build failed: Executable not found at $exePath" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit 1
}
