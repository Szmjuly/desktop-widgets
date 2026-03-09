<#
.SYNOPSIS
    Manage project tags in Firebase RTDB using HMAC-SHA256 hashed keys.

.DESCRIPTION
    Side script for DesktopHub project tagging system.
    Reads/writes tags to Firebase RTDB using the same HMAC secret and service account
    as the DesktopHub app. Project numbers are hashed so Firebase never stores plaintext identifiers.

.PARAMETER Action
    The action to perform: get, set, delete, list, import, export, show-secret

.PARAMETER ProjectNumber
    The project number (e.g., "2024337.00" or "P250784.00")

.PARAMETER TagKey
    Tag field key (e.g., "voltage", "hvac_type", "generator_brand")

.PARAMETER TagValue
    Tag value to set

.PARAMETER CsvFile
    Path to CSV file for import/export operations

.PARAMETER ServiceAccountPath
    Path to Firebase service account JSON. Defaults to secrets/firebase-license.json

.PARAMETER SecretPath
    Path to HMAC secret key file. Defaults to %LOCALAPPDATA%/DesktopHub/tag_secret.key

.EXAMPLE
    # View tags for a project
    .\tag-manager.ps1 -Action get -ProjectNumber "2024337.00"

.EXAMPLE
    # Set a single tag
    .\tag-manager.ps1 -Action set -ProjectNumber "2024337.00" -TagKey voltage -TagValue 208

.EXAMPLE
    # Bulk import from CSV
    .\tag-manager.ps1 -Action import -CsvFile tags.csv

.EXAMPLE
    # Export all tags to CSV
    .\tag-manager.ps1 -Action export -CsvFile tags_export.csv

.EXAMPLE
    # Show HMAC secret (Base64) for sharing with other machines
    .\tag-manager.ps1 -Action show-secret
#>

param(
    [Parameter(Mandatory)]
    [ValidateSet("get", "set", "delete", "list", "import", "export", "show-secret", "hash", "decrypt-dump")]
    [string]$Action,

    [string]$ProjectNumber,
    [string]$TagKey,
    [string]$TagValue,
    [string]$CsvFile,
    [string]$ServiceAccountPath,
    [string]$SecretPath
)

$ErrorActionPreference = "Stop"

# --- Defaults ---
$AppDataDir = Join-Path $env:LOCALAPPDATA "DesktopHub"
if (-not $SecretPath) {
    $SecretPath = Join-Path $AppDataDir "tag_secret.key"
}
if (-not $ServiceAccountPath) {
    $scriptDir = Split-Path -Parent $PSScriptRoot
    $ServiceAccountPath = Join-Path $scriptDir "secrets" "firebase-license.json"
    if (-not (Test-Path $ServiceAccountPath)) {
        $ServiceAccountPath = Join-Path $PSScriptRoot "firebase-license.json"
    }
}

$DatabaseUrl = "https://licenses-ff136-default-rtdb.firebaseio.com"
$FirebaseNode = "project_tags"

# --- HMAC Hashing ---
function Get-OrCreateSecret {
    if (Test-Path $SecretPath) {
        $bytes = [System.IO.File]::ReadAllBytes($SecretPath)
        if ($bytes.Length -ge 32) { return $bytes }
    }
    Write-Host "Generating new HMAC secret..." -ForegroundColor Yellow
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    $dir = Split-Path -Parent $SecretPath
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllBytes($SecretPath, $bytes)
    Write-Host "Secret saved to: $SecretPath" -ForegroundColor Green
    return $bytes
}

function Get-ProjectHash {
    param([string]$Number)
    $secret = Get-OrCreateSecret
    $normalized = $Number.Trim()
    if ($normalized -match "^[Pp](\d)") {
        $normalized = $normalized.Substring(1)
    }
    $normalized = $normalized.ToLowerInvariant()

    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = $secret
    $hashBytes = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($normalized))
    $hmac.Dispose()
    return ([BitConverter]::ToString($hashBytes) -replace '-','').ToLowerInvariant()
}

# --- Firebase Auth (Service Account JWT) ---
function Get-AccessToken {
    if (-not (Test-Path $ServiceAccountPath)) {
        throw "Service account file not found: $ServiceAccountPath"
    }
    $sa = Get-Content $ServiceAccountPath -Raw | ConvertFrom-Json

    # Build JWT
    $header = @{ alg = "RS256"; typ = "JWT" } | ConvertTo-Json -Compress
    $now = [int][double]::Parse(([DateTime]::UtcNow - [DateTime]::new(1970,1,1,0,0,0,'Utc')).TotalSeconds.ToString())
    $claims = @{
        iss   = $sa.client_email
        scope = "https://www.googleapis.com/auth/firebase.database https://www.googleapis.com/auth/userinfo.email"
        aud   = "https://oauth2.googleapis.com/token"
        iat   = $now
        exp   = $now + 3600
    } | ConvertTo-Json -Compress

    function ConvertTo-Base64Url([byte[]]$bytes) {
        [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_')
    }

    $headerB64  = ConvertTo-Base64Url([System.Text.Encoding]::UTF8.GetBytes($header))
    $claimsB64  = ConvertTo-Base64Url([System.Text.Encoding]::UTF8.GetBytes($claims))
    $unsigned   = "$headerB64.$claimsB64"

    $rsa = [System.Security.Cryptography.RSA]::Create()
    $rsa.ImportFromPem($sa.private_key)
    $sigBytes = $rsa.SignData(
        [System.Text.Encoding]::UTF8.GetBytes($unsigned),
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
    )
    $rsa.Dispose()
    $signature = ConvertTo-Base64Url $sigBytes
    $jwt = "$unsigned.$signature"

    $tokenResp = Invoke-RestMethod -Uri "https://oauth2.googleapis.com/token" -Method Post -Body @{
        grant_type = "urn:ietf:params:oauth:grant-type:jwt-bearer"
        assertion  = $jwt
    }
    return $tokenResp.access_token
}

# --- Firebase CRUD ---
function Invoke-FirebaseGet {
    param([string]$Path)
    $token = Get-AccessToken
    $url = "$DatabaseUrl/$Path.json?access_token=$token"
    try {
        return Invoke-RestMethod -Uri $url -Method Get
    } catch {
        if ($_.Exception.Response.StatusCode -eq 404) { return $null }
        throw
    }
}

function Invoke-FirebasePatch {
    param([string]$Path, [hashtable]$Data)
    $token = Get-AccessToken
    $url = "$DatabaseUrl/$Path.json?access_token=$token"
    $json = $Data | ConvertTo-Json -Depth 10 -Compress
    Invoke-RestMethod -Uri $url -Method Patch -Body $json -ContentType "application/json" | Out-Null
}

function Invoke-FirebasePut {
    param([string]$Path, $Data)
    $token = Get-AccessToken
    $url = "$DatabaseUrl/$Path.json?access_token=$token"
    $json = if ($null -eq $Data) { "null" } else { $Data | ConvertTo-Json -Depth 10 -Compress }
    Invoke-RestMethod -Uri $url -Method Put -Body $json -ContentType "application/json" | Out-Null
}

function Invoke-FirebaseDelete {
    param([string]$Path)
    $token = Get-AccessToken
    $url = "$DatabaseUrl/$Path.json?access_token=$token"
    Invoke-RestMethod -Uri $url -Method Delete | Out-Null
}

# --- AES-256-CBC Encryption/Decryption (matches TagValueEncryptor.cs) ---
function Get-EncryptionKey {
    $secret = Get-OrCreateSecret
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $key = $sha.ComputeHash($secret)
    $sha.Dispose()
    return $key
}

function Decrypt-TagValue {
    param([string]$CipherText)
    if (-not $CipherText) { return $CipherText }
    try {
        $combined = [Convert]::FromBase64String($CipherText)
        if ($combined.Length -lt 32) { return $CipherText } # Not encrypted
        
        $key = Get-EncryptionKey
        $aes = [System.Security.Cryptography.Aes]::Create()
        $aes.Key = $key
        $aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
        $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
        
        # Extract IV (first 16 bytes) and ciphertext (rest)
        $iv = $combined[0..15]
        $cipherBytes = $combined[16..($combined.Length - 1)]
        $aes.IV = $iv
        
        $decryptor = $aes.CreateDecryptor()
        $plainBytes = $decryptor.TransformFinalBlock($cipherBytes, 0, $cipherBytes.Length)
        $decryptor.Dispose()
        $aes.Dispose()
        
        return [System.Text.Encoding]::UTF8.GetString($plainBytes)
    } catch [System.FormatException] {
        return $CipherText # Not Base64, return as-is
    } catch [System.Security.Cryptography.CryptographicException] {
        return $CipherText # Decryption failed, likely plaintext
    } catch {
        return $CipherText # Any other error, return as-is
    }
}

function Encrypt-TagValue {
    param([string]$PlainText)
    if (-not $PlainText) { return $PlainText }
    try {
        $key = Get-EncryptionKey
        $aes = [System.Security.Cryptography.Aes]::Create()
        $aes.Key = $key
        $aes.GenerateIV()
        $aes.Mode = [System.Security.Cryptography.CipherMode]::CBC
        $aes.Padding = [System.Security.Cryptography.PaddingMode]::PKCS7
        
        $encryptor = $aes.CreateEncryptor()
        $plainBytes = [System.Text.Encoding]::UTF8.GetBytes($PlainText)
        $cipherBytes = $encryptor.TransformFinalBlock($plainBytes, 0, $plainBytes.Length)
        $encryptor.Dispose()
        
        # Prepend IV to ciphertext
        $result = New-Object byte[] ($aes.IV.Length + $cipherBytes.Length)
        [Array]::Copy($aes.IV, 0, $result, 0, $aes.IV.Length)
        [Array]::Copy($cipherBytes, 0, $result, $aes.IV.Length, $cipherBytes.Length)
        $aes.Dispose()
        
        return [Convert]::ToBase64String($result)
    } catch {
        Write-Host "Encrypt error: $_" -ForegroundColor Red
        return $PlainText
    }
}

function Decrypt-TagEntry {
    param($Tags)
    if (-not $Tags) { return $Tags }
    $result = @{}
    $Tags.PSObject.Properties | ForEach-Object {
        $key = $_.Name
        $val = $_.Value
        if ($val -is [array]) {
            $result[$key] = @($val | ForEach-Object { Decrypt-TagValue $_ })
        } elseif ($val -is [PSCustomObject]) {
            $inner = @{}
            $val.PSObject.Properties | ForEach-Object {
                $inner[$_.Name] = Decrypt-TagValue $_.Value
            }
            $result[$key] = $inner
        } elseif ($val -is [string]) {
            $result[$key] = Decrypt-TagValue $val
        } else {
            $result[$key] = $val
        }
    }
    return [PSCustomObject]$result
}

# --- Known tag fields ---
$KnownFields = @(
    "voltage", "phase", "amperage_service", "amperage_generator",
    "generator_brand", "generator_load_kw",
    "hvac_type", "hvac_brand", "hvac_tonnage", "hvac_load_kw",
    "square_footage", "build_type",
    "location_city", "location_state", "location_municipality", "location_address",
    "stamping_engineer", "engineers", "code_refs"
)

# --- Actions ---
switch ($Action) {
    "hash" {
        if (-not $ProjectNumber) { throw "ProjectNumber is required for hash action" }
        $hash = Get-ProjectHash $ProjectNumber
        Write-Host "Project: $ProjectNumber"
        Write-Host "Hash:    $hash"
    }

    "get" {
        if (-not $ProjectNumber) { throw "ProjectNumber is required for get action" }
        $hash = Get-ProjectHash $ProjectNumber
        Write-Host "Project: $ProjectNumber (hash: $($hash.Substring(0,12))...)" -ForegroundColor Cyan

        $data = Invoke-FirebaseGet "$FirebaseNode/$hash"
        if (-not $data) {
            Write-Host "No tags found." -ForegroundColor Yellow
            return
        }

        $tags = $data.tags
        if (-not $tags) {
            Write-Host "No tags found." -ForegroundColor Yellow
            return
        }

        # Decrypt tag values
        $decrypted = Decrypt-TagEntry $tags

        Write-Host ""
        foreach ($field in $KnownFields) {
            $val = $decrypted.$field
            if ($val) {
                $displayVal = if ($val -is [array]) { $val -join ", " } else { $val }
                Write-Host "  $($field.PadRight(24)) $displayVal" -ForegroundColor White
            }
        }

        # Custom tags
        if ($decrypted.custom) {
            Write-Host ""
            Write-Host "  Custom:" -ForegroundColor DarkCyan
            if ($decrypted.custom -is [hashtable]) {
                $decrypted.custom.GetEnumerator() | ForEach-Object {
                    Write-Host "    $($_.Key.PadRight(22)) $($_.Value)" -ForegroundColor Gray
                }
            } elseif ($decrypted.custom -is [PSCustomObject]) {
                $decrypted.custom.PSObject.Properties | ForEach-Object {
                    Write-Host "    $($_.Name.PadRight(22)) $($_.Value)" -ForegroundColor Gray
                }
            }
        }

        if ($data.updated_by) {
            Write-Host ""
            Write-Host "  Updated by: $($data.updated_by) at $($data.updated_at)" -ForegroundColor DarkGray
        }
    }

    "set" {
        if (-not $ProjectNumber) { throw "ProjectNumber is required for set action" }
        if (-not $TagKey) { throw "TagKey is required for set action" }
        if (-not $TagValue) { throw "TagValue is required for set action" }

        $hash = Get-ProjectHash $ProjectNumber
        $now = (Get-Date).ToUniversalTime().ToString("o")
        $username = $env:USERNAME.ToLowerInvariant()

        # Handle list fields
        $value = $TagValue
        if ($TagKey -in @("engineers", "code_refs") -and $TagValue -match ",") {
            $value = @($TagValue -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        }

        $tagData = @{ $TagKey = $value }
        Invoke-FirebasePatch "$FirebaseNode/$hash/tags" $tagData
        Invoke-FirebasePatch "$FirebaseNode/$hash" @{
            updated_by = $username
            updated_at = $now
        }

        Write-Host "Set $TagKey = $TagValue on $ProjectNumber (hash: $($hash.Substring(0,12))...)" -ForegroundColor Green
    }

    "delete" {
        if (-not $ProjectNumber) { throw "ProjectNumber is required for delete action" }
        $hash = Get-ProjectHash $ProjectNumber

        if ($TagKey) {
            # Delete single tag field
            Invoke-FirebasePut "$FirebaseNode/$hash/tags/$TagKey" $null
            Write-Host "Deleted tag '$TagKey' from $ProjectNumber" -ForegroundColor Yellow
        } else {
            # Delete all tags for project
            $confirm = Read-Host "Delete ALL tags for $ProjectNumber? (yes/no)"
            if ($confirm -ne "yes") {
                Write-Host "Cancelled." -ForegroundColor Gray
                return
            }
            Invoke-FirebaseDelete "$FirebaseNode/$hash"
            Write-Host "Deleted all tags for $ProjectNumber" -ForegroundColor Yellow
        }
    }

    "list" {
        Write-Host "Fetching all project tags from Firebase..." -ForegroundColor Cyan
        $allData = Invoke-FirebaseGet $FirebaseNode
        if (-not $allData) {
            Write-Host "No tags found in database." -ForegroundColor Yellow
            return
        }

        $count = 0
        $allData.PSObject.Properties | ForEach-Object {
            $hash = $_.Name
            $entry = $_.Value
            $count++
            $updatedBy = if ($entry.updated_by) { $entry.updated_by } else { "unknown" }
            $tagCount = 0
            if ($entry.tags) {
                $tagCount = ($entry.tags.PSObject.Properties | Measure-Object).Count
            }
            Write-Host "  $($hash.Substring(0,16))...  $tagCount tags  (by $updatedBy)" -ForegroundColor White
        }
        Write-Host ""
        Write-Host "$count total project tag entries" -ForegroundColor Cyan
    }

    "import" {
        if (-not $CsvFile) { throw "CsvFile is required for import action" }
        if (-not (Test-Path $CsvFile)) { throw "CSV file not found: $CsvFile" }

        $csv = Import-Csv $CsvFile
        $username = $env:USERNAME.ToLowerInvariant()
        $now = (Get-Date).ToUniversalTime().ToString("o")

        # CSV must have a 'project_number' column; all other columns are tag fields
        $imported = 0
        foreach ($row in $csv) {
            $projNum = $row.project_number
            if (-not $projNum) {
                Write-Host "Skipping row without project_number" -ForegroundColor Yellow
                continue
            }

            $hash = Get-ProjectHash $projNum
            $tagData = @{}

            $row.PSObject.Properties | Where-Object { $_.Name -ne "project_number" -and $_.Value } | ForEach-Object {
                $key = $_.Name.ToLowerInvariant().Replace(" ", "_")
                $val = $_.Value.Trim()
                if ($val) {
                    if ($key -in @("engineers", "code_refs") -and $val -match ",") {
                        $tagData[$key] = @($val -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
                    } else {
                        $tagData[$key] = $val
                    }
                }
            }

            if ($tagData.Count -gt 0) {
                Invoke-FirebasePatch "$FirebaseNode/$hash/tags" $tagData
                Invoke-FirebasePatch "$FirebaseNode/$hash" @{
                    updated_by = $username
                    updated_at = $now
                }
                $imported++
                Write-Host "  Imported $projNum ($($tagData.Count) fields)" -ForegroundColor Green
            }
        }
        Write-Host ""
        Write-Host "$imported projects imported." -ForegroundColor Cyan
    }

    "export" {
        if (-not $CsvFile) { throw "CsvFile is required for export action" }

        # We need the local cache to reverse-map hashes to project numbers
        $cachePath = Join-Path $AppDataDir "tag_cache.json"
        $hashToNumber = @{}
        if (Test-Path $cachePath) {
            $cache = Get-Content $cachePath -Raw | ConvertFrom-Json
            foreach ($entry in $cache.Entries) {
                if ($entry.Hash -and $entry.ProjectNumber) {
                    $hashToNumber[$entry.Hash] = $entry.ProjectNumber
                }
            }
        }

        Write-Host "Fetching all tags from Firebase..." -ForegroundColor Cyan
        $allData = Invoke-FirebaseGet $FirebaseNode
        if (-not $allData) {
            Write-Host "No tags found." -ForegroundColor Yellow
            return
        }

        $rows = @()
        $allData.PSObject.Properties | ForEach-Object {
            $hash = $_.Name
            $entry = $_.Value
            $projNum = if ($hashToNumber.ContainsKey($hash)) { $hashToNumber[$hash] } else { "UNKNOWN_$($hash.Substring(0,12))" }

            $row = [ordered]@{ project_number = $projNum }
            foreach ($field in $KnownFields) {
                $val = $entry.tags.$field
                $row[$field] = if ($val -is [array]) { $val -join ", " } else { $val }
            }
            $rows += [PSCustomObject]$row
        }

        $rows | Export-Csv -Path $CsvFile -NoTypeInformation
        Write-Host "Exported $($rows.Count) projects to $CsvFile" -ForegroundColor Green
    }

    "decrypt-dump" {
        Write-Host "Fetching and decrypting all project tags from Firebase..." -ForegroundColor Cyan
        $allData = Invoke-FirebaseGet $FirebaseNode
        if (-not $allData) {
            Write-Host "No tags found in database." -ForegroundColor Yellow
            return
        }

        # Load local cache for reverse hash mapping
        $cachePath = Join-Path $AppDataDir "tag_cache.json"
        $hashToNumber = @{}
        if (Test-Path $cachePath) {
            $cache = Get-Content $cachePath -Raw | ConvertFrom-Json
            foreach ($entry in $cache.Entries) {
                if ($entry.Hash -and $entry.ProjectNumber) {
                    $hashToNumber[$entry.Hash] = $entry.ProjectNumber
                }
            }
        }

        $count = 0
        $allData.PSObject.Properties | ForEach-Object {
            $hash = $_.Name
            $entry = $_.Value
            $count++

            $projNum = if ($hashToNumber.ContainsKey($hash)) { $hashToNumber[$hash] } else { "UNKNOWN_$($hash.Substring(0,12))" }
            Write-Host "`n=== $projNum (hash: $($hash.Substring(0,16))...) ===" -ForegroundColor Cyan

            if ($entry.tags) {
                $decrypted = Decrypt-TagEntry $entry.tags
                foreach ($field in $KnownFields) {
                    $val = $decrypted.$field
                    if ($val) {
                        $displayVal = if ($val -is [array]) { $val -join ", " } else { $val }
                        Write-Host "  $($field.PadRight(24)) $displayVal" -ForegroundColor White
                    }
                }
                if ($decrypted.custom) {
                    Write-Host "  Custom:" -ForegroundColor DarkCyan
                    if ($decrypted.custom -is [hashtable]) {
                        $decrypted.custom.GetEnumerator() | ForEach-Object {
                            Write-Host "    $($_.Key.PadRight(22)) $($_.Value)" -ForegroundColor Gray
                        }
                    }
                }
            }

            if ($entry.updated_by) {
                Write-Host "  Updated by: $($entry.updated_by) at $($entry.updated_at)" -ForegroundColor DarkGray
            }
        }
        Write-Host "`n$count total entries decrypted." -ForegroundColor Cyan
    }

    "show-secret" {
        $secret = Get-OrCreateSecret
        $b64 = [Convert]::ToBase64String($secret)
        Write-Host "HMAC Secret (Base64):" -ForegroundColor Cyan
        Write-Host $b64 -ForegroundColor White
        Write-Host ""
        Write-Host "To use this secret on another machine, save it to:" -ForegroundColor Gray
        Write-Host "  $SecretPath" -ForegroundColor Gray
        Write-Host ""
        Write-Host "Or run:" -ForegroundColor Gray
        Write-Host "  [System.IO.File]::WriteAllBytes('$SecretPath', [Convert]::FromBase64String('$b64'))" -ForegroundColor DarkGray
    }
}
