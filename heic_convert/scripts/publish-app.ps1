# Publishes the WPF desktop UI ONLY (HeicConvert.exe with drag-and-drop window).
# Does NOT publish the command-line tool (HeicConvert.Cli).
#
# If you ran dotnet publish on the whole solution, you also got HeicConvert.Cli.exe.
# Use this script or dotnet publish --project HeicConvert.App only for the UI.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$proj = Join-Path $root 'HeicConvert.App\HeicConvert.App.csproj'
if (-not (Test-Path $proj)) {
    throw "UI project not found: $proj"
}

$outDir = Join-Path $root 'publish-ui'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host 'Publishing WPF UI: HeicConvert.App -> single-file HeicConvert.exe ...' -ForegroundColor Cyan

dotnet publish $proj `
    -c Release `
    -f net8.0-windows `
    -p:PublishProfile=Win64-SingleFile `
    -o $outDir

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $outDir 'HeicConvert.exe'
if (Test-Path $exe) {
    $len = (Get-Item $exe).Length / 1MB
    Write-Host ""
    Write-Host "Desktop app (this is the one you want): $exe ($([math]::Round($len, 1)) MB)" -ForegroundColor Green
} else {
    Write-Warning "Expected UI output not found: $exe"
}

$cliLeftover = Join-Path $outDir 'HeicConvert.Cli.exe'
if (Test-Path $cliLeftover) {
    Write-Warning 'HeicConvert.Cli.exe is in this folder; you may have mixed a solution publish with this output folder. The UI is still HeicConvert.exe.'
}
