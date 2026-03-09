# Setup .NET SDK path for DesktopHub development
$dotnetPath = "C:\dotnet"

# Check if dotnet exists at the specified path
if (Test-Path "$dotnetPath\dotnet.exe") {
    # Add to current session PATH
    $env:PATH = "$dotnetPath;$env:PATH"
    
    # Add to user PATH permanently
    $currentPath = [Environment]::GetEnvironmentVariable("PATH", "User")
    if ($currentPath -notlike "*$dotnetPath*") {
        [Environment]::SetEnvironmentVariable("PATH", "$currentPath;$dotnetPath", "User")
        Write-Host "✅ Added $dotnetPath to user PATH permanently" -ForegroundColor Green
    } else {
        Write-Host "✅ $dotnetPath already in PATH" -ForegroundColor Green
    }
    
    # Verify installation
    $version = & "$dotnetPath\dotnet.exe" --version
    Write-Host "✅ .NET SDK version: $version" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now run: .\build-single-file.ps1" -ForegroundColor Cyan
} else {
    Write-Host "❌ .NET SDK not found at $dotnetPath" -ForegroundColor Red
    Write-Host "Please extract the .NET SDK zip to C:\dotnet first" -ForegroundColor Yellow
}
