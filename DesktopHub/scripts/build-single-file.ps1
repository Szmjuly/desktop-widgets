# Build DesktopHub as a single-file executable
Write-Host "Building DesktopHub as single-file executable..." -ForegroundColor Green

$projectPath = "src\DesktopHub.UI\DesktopHub.UI.csproj"
$outputDir = "publish"

# Clean previous build
Write-Host "Cleaning previous build..." -ForegroundColor Yellow
dotnet clean $projectPath -c Release

# Build and publish as single file
Write-Host "Building and publishing as single file..." -ForegroundColor Yellow
dotnet publish $projectPath -c Release -p:PublishProfile=SingleFile -o $outputDir

# Check if build succeeded
if ($LASTEXITCODE -eq 0) {
    $exePath = Join-Path $outputDir "DesktopHub.UI.exe"
    if (Test-Path $exePath) {
        $fileSize = (Get-Item $exePath).Length / 1MB
        Write-Host "✅ Build successful!" -ForegroundColor Green
        Write-Host "📁 Output: $exePath" -ForegroundColor Cyan
        Write-Host "📊 Size: {0:F2} MB" -f $fileSize -ForegroundColor Cyan
        Write-Host ""
        Write-Host "You can now run the executable directly:" -ForegroundColor White
        Write-Host "  $exePath" -ForegroundColor Gray
    } else {
        Write-Host "❌ Build failed: Executable not found" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "❌ Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit 1
}
