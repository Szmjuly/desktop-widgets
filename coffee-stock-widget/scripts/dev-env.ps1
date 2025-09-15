# Non-admin developer environment bootstrap for Coffee Stock Widget
# - Installs a local .NET SDK into ./.dotnet (if needed)
# - Adds it to PATH for this PowerShell session
# - Creates wrapper functions

$ErrorActionPreference = "Stop"

# Repo root based on this script's location
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnetDir = Join-Path $repoRoot ".dotnet"
$dotnetExe = Join-Path $dotnetDir "dotnet.exe"
$installScript = Join-Path $dotnetDir "dotnet-install.ps1"

# Ensure folders
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
    $uri = "https://dot.net/v1/dotnet-install.ps1"
    Invoke-WebRequest -UseBasicParsing -Uri $uri -OutFile $installScript
}

# Install SDK if not present or version mismatch
$installNeeded = $true
if (Test-Path $dotnetExe) {
    try {
        $current = & $dotnetExe --version
        if ($desiredVersion -and $current.Trim() -eq $desiredVersion.Trim()) {
            $installNeeded = $false
        }
    } catch { }
}

if ($installNeeded) {
    Write-Host "Installing .NET SDK ($desiredVersion) locally to .dotnet/..." -ForegroundColor Cyan
    try {
        if ($desiredVersion) {
            & $installScript -InstallDir "$dotnetDir" -Version "$desiredVersion"
        } else {
            & $installScript -InstallDir "$dotnetDir" -Channel "8.0" -Quality "ga"
        }
    } catch {
        Write-Warning "Primary install attempt failed: $($_.Exception.Message)"
        Write-Warning "Retrying with channel 8.0 and quality ga..."
        & $installScript -InstallDir "$dotnetDir" -Channel "8.0" -Quality "ga"
    }
}

# Add to PATH for current session and create wrapper
$env:PATH = "$dotnetDir;$env:PATH"
function dotnet { & "$dotnetExe" $args }

Write-Host "Using dotnet from: $dotnetExe" -ForegroundColor Green
try {
    dotnet --info | Out-Host
} catch {
    Write-Warning "dotnet did not run as expected. Ensure ExecutionPolicy allows this script in the current session. Try: Set-ExecutionPolicy -Scope Process Bypass"
}

Write-Host "Environment ready. This applies to the current PowerShell session only." -ForegroundColor Green
