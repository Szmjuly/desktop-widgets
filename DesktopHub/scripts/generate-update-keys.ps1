#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Generate the RSA keypair used to sign DesktopHub auto-updates.

.DESCRIPTION
  Creates a 3072-bit RSA keypair:
    - .signing/desktophub-updates-private.pem    (gitignored, KEEP THIS)
    - assets/update-keys/current.pub.pem         (committed, embedded in client)

  Uses openssl if present; otherwise generates the key in-process via .NET.
  Sets up $env:DH_SIGNING_KEY for the current user so build-single-file.ps1
  can find the private key automatically.

.PARAMETER Force
  Overwrite the existing private key. By default the script refuses if
  .signing/desktophub-updates-private.pem already exists.

.PARAMETER PublicKeyName
  File name (without path) for the public key under assets/update-keys/.
  Default is "current.pub.pem". Use "next.pub.pem" during a rotation.

.EXAMPLE
  # First-time setup:
  ./scripts/generate-update-keys.ps1

.EXAMPLE
  # Rotation (adds a second key alongside current):
  ./scripts/generate-update-keys.ps1 -PublicKeyName "next.pub.pem"
#>
param(
    [switch]$Force,
    [string]$PublicKeyName = "current.pub.pem"
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$signingDir = Join-Path $repoRoot ".signing"
$keysDir    = Join-Path $repoRoot "assets\update-keys"
$privPath   = Join-Path $signingDir "desktophub-updates-private.pem"
$pubPath    = Join-Path $keysDir $PublicKeyName

New-Item -ItemType Directory -Force -Path $signingDir | Out-Null
New-Item -ItemType Directory -Force -Path $keysDir    | Out-Null

if ((Test-Path $privPath) -and -not $Force) {
    Write-Host ""
    Write-Host "Private key already exists: $privPath" -ForegroundColor Yellow
    Write-Host "Refusing to overwrite. Pass -Force if you really mean to replace it."
    Write-Host "(Replacing the private key will break existing installs that only" -ForegroundColor DarkYellow
    Write-Host " embed the matching public key -- see assets/update-keys/README.md)" -ForegroundColor DarkYellow
    exit 1
}

function Find-OpenSSL {
    $cmd = Get-Command openssl -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $candidates = @(
        "C:\Program Files\Git\usr\bin\openssl.exe",
        "C:\Program Files\Git\mingw64\bin\openssl.exe",
        "C:\Program Files (x86)\Git\usr\bin\openssl.exe",
        "C:\Program Files (x86)\Git\mingw64\bin\openssl.exe",
        "$env:LOCALAPPDATA\Programs\Git\usr\bin\openssl.exe",
        "$env:LOCALAPPDATA\Programs\Git\mingw64\bin\openssl.exe",
        "C:\Program Files\OpenSSL-Win64\bin\openssl.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}

$openssl = Find-OpenSSL
if ($openssl) {
    Write-Host "Using openssl at: $openssl" -ForegroundColor Cyan
    # openssl writes info to stderr ("writing RSA key"). Swallow stderr — rely on exit code.
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $openssl genrsa -out $privPath 3072 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "openssl genrsa failed" }
        & $openssl rsa -in $privPath -pubout -out $pubPath 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "openssl rsa -pubout failed" }
    } finally {
        $ErrorActionPreference = $prevEAP
    }
}
else {
    Write-Host "openssl not found -- falling back to in-process .NET key generation." -ForegroundColor Cyan

    function Export-PemBlock([byte[]]$bytes, [string]$label) {
        $b64 = [Convert]::ToBase64String($bytes, [Base64FormattingOptions]::InsertLineBreaks)
        "-----BEGIN $label-----`r`n$b64`r`n-----END $label-----`r`n"
    }

    $rsa = $null
    try {
        $rsa = [System.Security.Cryptography.RSA]::Create(3072)
        try {
            $privBytes = $rsa.ExportPkcs8PrivateKey()
            $pubBytes  = $rsa.ExportSubjectPublicKeyInfo()
        } catch {
            Write-Host ""
            Write-Host "This PowerShell version is too old to export PKCS#8 keys natively." -ForegroundColor Red
            Write-Host "Install Git for Windows (ships with openssl) or install PowerShell 7+," -ForegroundColor Red
            Write-Host "then re-run this script:" -ForegroundColor Red
            Write-Host "  winget install --id Git.Git -e --source winget" -ForegroundColor Gray
            Write-Host "  winget install --id Microsoft.PowerShell -e --source winget" -ForegroundColor Gray
            throw "Unsupported PowerShell/.NET version -- see message above."
        }

        [System.IO.File]::WriteAllText($privPath, (Export-PemBlock $privBytes "PRIVATE KEY"))
        [System.IO.File]::WriteAllText($pubPath,  (Export-PemBlock $pubBytes  "PUBLIC KEY"))
    }
    finally {
        if ($null -ne $rsa) { $rsa.Dispose() }
    }
}

Write-Host ""
Write-Host "Private key: $privPath" -ForegroundColor Green
Write-Host "Public key:  $pubPath" -ForegroundColor Green
Write-Host ""

# Persist DH_SIGNING_KEY for future PowerShell sessions (user scope).
[System.Environment]::SetEnvironmentVariable("DH_SIGNING_KEY", $privPath, "User")
$env:DH_SIGNING_KEY = $privPath
Write-Host "DH_SIGNING_KEY set (user scope) -> $privPath" -ForegroundColor Green
Write-Host ""

Write-Host "NEXT STEPS" -ForegroundColor Yellow
Write-Host "  1. Back up the private key to your password manager right now:"
Write-Host "     notepad `"$privPath`"" -ForegroundColor Gray
Write-Host "     (copy the whole PEM, paste into a 1Password/Bitwarden secure note)"
Write-Host ""
Write-Host "  2. Verify the build pipeline:"
Write-Host "     ./scripts/build-single-file.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Commit the public key (public halves are safe to commit):"
Write-Host "     git add assets/update-keys/$PublicKeyName" -ForegroundColor Gray
Write-Host ""
Write-Host "See assets/update-keys/README.md for rotation procedure and full details."
