#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Sign a release artifact with the DesktopHub update-signing RSA key.

.DESCRIPTION
  Produces a detached signature file next to the input: <ExePath>.sig.
  The signature is RSA-SHA256 (PKCS#1 v1.5) over the raw bytes of the input.
  Clients verify the signature against an embedded public key (see
  src/DesktopHub.UI/Services/UpdateVerifier.cs).

.PARAMETER ExePath
  Path to the file to sign (typically publish/DesktopHub.exe).

.PARAMETER PrivateKeyPath
  Path to an unencrypted PEM-encoded RSA private key (PKCS#8 preferred;
  traditional RSA PEM also supported). Defaults to $env:DH_SIGNING_KEY.

.EXAMPLE
  ./scripts/sign-update.ps1 -ExePath publish/DesktopHub.exe -PrivateKeyPath "$env:USERPROFILE\.desktophub\signing-key.pem"

.NOTES
  Never commit the private key. See assets/update-keys/README.md for setup.
  Works in both Windows PowerShell 5.1 and PowerShell 7+.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [string]$PrivateKeyPath = $env:DH_SIGNING_KEY
)

$ErrorActionPreference = 'Stop'

function Import-RsaPrivateKey([string]$pemText) {
    $body = $pemText `
        -replace '-----BEGIN PRIVATE KEY-----', '' `
        -replace '-----END PRIVATE KEY-----', '' `
        -replace '-----BEGIN RSA PRIVATE KEY-----', '' `
        -replace '-----END RSA PRIVATE KEY-----', '' `
        -replace '\s+', ''
    $keyBytes = [Convert]::FromBase64String($body)

    # Try modern PKCS#8 import first (.NET Core / .NET 5+)
    try {
        $rsa = [System.Security.Cryptography.RSA]::Create()
        $bytesRead = 0
        $rsa.ImportPkcs8PrivateKey($keyBytes, [ref]$bytesRead)
        return $rsa
    } catch { }

    # Try RSA-specific key import
    try {
        $rsa = [System.Security.Cryptography.RSA]::Create()
        $bytesRead = 0
        $rsa.ImportRSAPrivateKey($keyBytes, [ref]$bytesRead)
        return $rsa
    } catch { }

    # Fallback for .NET Framework 4.x (Windows PowerShell 5.1) — use CngKey
    try {
        $cng = [System.Security.Cryptography.CngKey]::Import(
            $keyBytes,
            [System.Security.Cryptography.CngKeyBlobFormat]::Pkcs8PrivateBlob)
        return [System.Security.Cryptography.RSACng]::new($cng)
    } catch { }

    throw "Could not import private key. Ensure it is an unencrypted PEM-encoded RSA key (PKCS#8 preferred)."
}

function Import-RsaPublicKey([string]$pemText) {
    $body = $pemText `
        -replace '-----BEGIN PUBLIC KEY-----', '' `
        -replace '-----END PUBLIC KEY-----', '' `
        -replace '-----BEGIN RSA PUBLIC KEY-----', '' `
        -replace '-----END RSA PUBLIC KEY-----', '' `
        -replace '\s+', ''
    $keyBytes = [Convert]::FromBase64String($body)

    try {
        $rsa = [System.Security.Cryptography.RSA]::Create()
        $bytesRead = 0
        $rsa.ImportSubjectPublicKeyInfo($keyBytes, [ref]$bytesRead)
        return $rsa
    } catch { }

    try {
        $rsa = [System.Security.Cryptography.RSA]::Create()
        $bytesRead = 0
        $rsa.ImportRSAPublicKey($keyBytes, [ref]$bytesRead)
        return $rsa
    } catch { }

    throw "Could not import public key (unsupported PEM format)."
}

if (-not (Test-Path $ExePath)) {
    throw "ExePath not found: $ExePath"
}

if ([string]::IsNullOrWhiteSpace($PrivateKeyPath)) {
    throw "No private key supplied. Pass -PrivateKeyPath or set `$env:DH_SIGNING_KEY."
}

if (-not (Test-Path $PrivateKeyPath)) {
    throw "PrivateKeyPath not found: $PrivateKeyPath"
}

Write-Host "Signing: $ExePath"
Write-Host "With key: $PrivateKeyPath"

$bytes = [System.IO.File]::ReadAllBytes($ExePath)
$pem = Get-Content -Raw -Path $PrivateKeyPath
$rsa = Import-RsaPrivateKey $pem

$signature = $rsa.SignData(
    $bytes,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
)

$sigPath = "$ExePath.sig"
[System.IO.File]::WriteAllBytes($sigPath, $signature)

Write-Host "Wrote detached signature: $sigPath ($($signature.Length) bytes)"

# Sanity check: verify immediately against the matching public key if one is
# sitting in assets/update-keys. This catches "wrong key file" before upload.
# Public-key import requires .NET Core APIs, so on Windows PowerShell 5.1 we
# skip this check — the client (.NET 8) will do the real verification anyway.
$supportsSpkiImport = $null -ne ([System.Security.Cryptography.RSA].GetMethod(
    "ImportSubjectPublicKeyInfo",
    [Reflection.BindingFlags]'Public,Instance'))
$publicKeys = Get-ChildItem -Path (Join-Path $PSScriptRoot "..\assets\update-keys") -Filter "*.pub.pem" -ErrorAction SilentlyContinue
if (-not $publicKeys) {
    Write-Warning "No embedded public keys found in assets/update-keys/."
    Write-Warning "Clients will REFUSE all updates. Run ./scripts/generate-update-keys.ps1 first."
}
elseif (-not $supportsSpkiImport) {
    Write-Host "Skipping in-script self-verify (needs PowerShell 7+). Use openssl to double-check:" -ForegroundColor DarkGray
    foreach ($pub in $publicKeys) {
        Write-Host "  openssl dgst -sha256 -verify `"$($pub.FullName)`" -signature `"$sigPath`" `"$ExePath`"" -ForegroundColor DarkGray
    }
}
else {
    $verified = $false
    foreach ($pub in $publicKeys) {
        try {
            $pubRsa = Import-RsaPublicKey (Get-Content -Raw $pub.FullName)
            if ($pubRsa.VerifyData(
                    $bytes,
                    $signature,
                    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
                    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)) {
                Write-Host "Verified against embedded public key: $($pub.Name)"
                $verified = $true
                break
            }
        } catch { }
    }
    if (-not $verified) {
        Write-Warning "Signature did NOT verify against any embedded public key in assets/update-keys/."
        Write-Warning "Clients will REJECT this update. Make sure the public half of $PrivateKeyPath is embedded."
    }
}
