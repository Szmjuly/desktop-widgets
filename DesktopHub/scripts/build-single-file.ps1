# Build DesktopHub as a single-file executable
Write-Host "Building DesktopHub as single-file executable..." -ForegroundColor Green

$projectRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $projectRoot "src\DesktopHub.UI\DesktopHub.UI.csproj"
$outputDir = Join-Path $projectRoot "publish"

# Clean previous build
Write-Host "Cleaning previous build..." -ForegroundColor Yellow
dotnet clean $projectPath -c Release

# Build and publish as single file
Write-Host "Building and publishing as single file..." -ForegroundColor Yellow
dotnet publish $projectPath -c Release -p:PublishProfile=SingleFile -o $outputDir

# Check if build succeeded
if ($LASTEXITCODE -eq 0) {
    $exePath = Join-Path $outputDir "DesktopHub.exe"
    if (Test-Path $exePath) {
        $fileSize = (Get-Item $exePath).Length / 1MB
        Write-Host "✅ Build successful!" -ForegroundColor Green
        Write-Host "📁 Output: $exePath" -ForegroundColor Cyan
        Write-Host ("📊 Size: {0:F2} MB" -f $fileSize) -ForegroundColor Cyan

        # Sign the release so the client's UpdateVerifier will accept it.
        # Skipped silently if no signing key is configured (dev builds),
        # but release uploads MUST have the .sig or clients will refuse.
        if ($env:DH_SIGNING_KEY -and (Test-Path $env:DH_SIGNING_KEY)) {
            Write-Host ""
            Write-Host "Signing release artifact..." -ForegroundColor Yellow
            & (Join-Path $PSScriptRoot "sign-update.ps1") -ExePath $exePath -PrivateKeyPath $env:DH_SIGNING_KEY
            if ($LASTEXITCODE -ne 0) {
                Write-Host "❌ Signing failed." -ForegroundColor Red
                exit 1
            }
        } else {
            Write-Warning "DH_SIGNING_KEY not set — build is NOT signed. Do not upload this EXE as a release."
            Write-Warning "See assets/update-keys/README.md for signing setup."
        }

        Write-Host ""
        Write-Host "You can now run the executable directly:" -ForegroundColor White
        Write-Host "  $exePath" -ForegroundColor Gray
        Write-Host ""
        Write-Host "To install with Start Menu integration:" -ForegroundColor Cyan
        Write-Host "  .\install-desktophub.ps1" -ForegroundColor Gray
    } else {
        Write-Host "❌ Build failed: Executable not found at $exePath" -ForegroundColor Red
        Write-Host "Project root: $projectRoot" -ForegroundColor Yellow
        Write-Host "Project path: $projectPath" -ForegroundColor Yellow
        Write-Host "Output dir: $outputDir" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "❌ Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    Write-Host "Project root: $projectRoot" -ForegroundColor Yellow
    Write-Host "Project path: $projectPath" -ForegroundColor Yellow
    exit 1
}
