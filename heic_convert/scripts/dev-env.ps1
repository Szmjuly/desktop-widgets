$ErrorActionPreference = "Stop"

# Repo root based on this script's location
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnetDir = Join-Path $repoRoot ".dotnet"
$dotnetExe = Join-Path $dotnetDir "dotnet.exe"
$installScript = Join-Path $dotnetDir "dotnet-install.ps1"

# Ensure folder exists
if (!(Test-Path $dotnetDir)) { New-Item -ItemType Directory -Path $dotnetDir | Out-Null }

# Try to read desired SDK from global.json
$globalJsonPath = Join-Path $repoRoot "global.json"
$desiredVersion = $null
if (Test-Path $globalJsonPath) {
    try {
        $gj = Get-Content $globalJsonPath | ConvertFrom-Json
        $desiredVersion = $gj.sdk.version
    } catch { }
}

# Download dotnet-install.ps1 if missing
if (!(Test-Path $installScript)) {
    Write-Host "Downloading dotnet-install.ps1..." -ForegroundColor Cyan
    Invoke-WebRequest -UseBasicParsing -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
}

# Install SDK if missing or version mismatch
$installNeeded = $true
if (Test-Path $dotnetExe) {
    try {
        $current = & $dotnetExe --version
        if ($desiredVersion -and $current.Trim() -eq $desiredVersion.Trim()) {
            $installNeeded = $false
        } elseif (-not $desiredVersion) {
            $installNeeded = $false
        }
    } catch { }
}

if ($installNeeded) {
    Write-Host "Installing .NET SDK locally to .dotnet/..." -ForegroundColor Cyan
    try {
        if ($desiredVersion) {
            & $installScript -InstallDir "$dotnetDir" -Version "$desiredVersion"
        } else {
            & $installScript -InstallDir "$dotnetDir" -Channel "8.0" -Quality "ga"
        }
    } catch {
        Write-Warning "Primary install attempt failed: $($_.Exception.Message)"
        Write-Warning "Retrying with channel 8.0 (GA)..."
        & $installScript -InstallDir "$dotnetDir" -Channel "8.0" -Quality "ga"
    }
}

# Add local SDK to PATH for current session and create wrapper
$env:PATH = "$dotnetDir;$env:PATH"
function dotnet { & "$dotnetExe" $args }

Write-Host "Using dotnet from: $dotnetExe" -ForegroundColor Green
try {
    dotnet --info | Out-Host
} catch {
    Write-Warning "dotnet did not run as expected. Run: Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass"
}

Write-Host "Environment ready for this PowerShell session." -ForegroundColor Green
