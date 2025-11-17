# Non-admin developer environment bootstrap for Portfolio_platform
# - Verifies Python is available
# - Sets up virtual environment in project folder if needed
# - Installs dependencies if not already installed

$ErrorActionPreference = "Stop"

# Repo root based on this script's location
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$venvDir = Join-Path $repoRoot ".venv"
$venvPython = Join-Path $venvDir "Scripts\python.exe"
$requirementsFile = Join-Path $repoRoot "requirements.txt"

# Check for Python in PATH first, preferring 3.11/3.12 over 3.14
$pythonCmd = $null
$foundVersions = @()

# First, try to find all Python versions via py launcher
try {
    $pyList = & py -0 2>&1
    if ($LASTEXITCODE -eq 0 -and $pyList) {
        $pyList | ForEach-Object {
            if ($_ -match '-(\d+)\.(\d+)') {
                $major = $matches[1]
                $minor = $matches[2]
                $ver = "$major.$minor"
                try {
                    $versionOutput = & py "-$ver" --version 2>&1
                    if ($LASTEXITCODE -eq 0) {
                        $versionNum = ($versionOutput -replace 'Python ', '').Split('.')[0..1] -join '.'
                        $foundVersions += [PSCustomObject]@{
                            Command = "py -$ver"
                            Version = [version]$versionNum
                            VersionString = $versionOutput.Trim()
                        }
                    }
                } catch {
                    continue
                }
            }
        }
    }
} catch {
    # If py launcher doesn't work, try common versions
    @("3.12", "3.11", "3.13", "3.14", "3.10") | ForEach-Object {
        try {
            $versionOutput = & py "-$_" --version 2>&1
            if ($LASTEXITCODE -eq 0) {
                $versionNum = ($versionOutput -replace 'Python ', '').Split('.')[0..1] -join '.'
                $foundVersions += [PSCustomObject]@{
                    Command = "py -$_"
                    Version = [version]$versionNum
                    VersionString = $versionOutput.Trim()
                }
            }
        } catch {
            continue
        }
    }
}

# Also check direct commands
$pythonVersions = @("python3.12", "python3.11", "python3", "python")
foreach ($cmd in $pythonVersions) {
    try {
        $version = & $cmd --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $versionNum = ($version -replace 'Python ', '').Split('.')[0..1] -join '.'
            $foundVersions += [PSCustomObject]@{
                Command = $cmd
                Version = [version]$versionNum
                VersionString = $version.Trim()
            }
        }
    } catch {
        continue
    }
}

# Select best version: prefer 3.12, then 3.11, then 3.13, avoid 3.14 if others exist
if ($foundVersions.Count -gt 0) {
    $preferred = $foundVersions | Where-Object { 
        $_.Version -ge [version]"3.11" -and $_.Version -lt [version]"3.14" 
    } | Sort-Object @{Expression = {
        if ($_.Version -ge [version]"3.12" -and $_.Version -lt [version]"3.13") { 1 }
        elseif ($_.Version -ge [version]"3.11" -and $_.Version -lt [version]"3.12") { 2 }
        else { 3 }
    }}, Version -Descending | Select-Object -First 1
    
    if ($preferred) {
        $pythonCmd = $preferred.Command
        Write-Host "Found Python: $($preferred.VersionString)" -ForegroundColor Green
        Write-Host "Selected: $($preferred.Command)" -ForegroundColor Cyan
    } else {
        # Fall back to latest version
        $latest = $foundVersions | Sort-Object Version -Descending | Select-Object -First 1
        $pythonCmd = $latest.Command
        Write-Host "Found Python: $($latest.VersionString)" -ForegroundColor Green
        $versionNum = $latest.Version
        if ($versionNum -ge [version]"3.14") {
            Write-Host "Note: Python 3.14 is very new - some packages may not have pre-built wheels yet." -ForegroundColor Yellow
            Write-Host "Consider using Python 3.12 for better package compatibility." -ForegroundColor Yellow
        }
    }
}

# If not in PATH, check common installation locations (including Microsoft Store)
if (-not $pythonCmd) {
    Write-Host "Python not found in PATH. Checking common installation locations..." -ForegroundColor Yellow
    
    $possiblePaths = @(
        # Microsoft Store Python locations (check 3.12 first)
        "$env:LOCALAPPDATA\Microsoft\WindowsApps\python3.12.exe",
        "$env:LOCALAPPDATA\Microsoft\WindowsApps\python3.11.exe",
        "$env:LOCALAPPDATA\Microsoft\WindowsApps\python3.exe",
        "$env:LOCALAPPDATA\Microsoft\WindowsApps\python.exe",
        # Other common locations
        "C:\Users\smarkowitz\python\python.exe",
        "C:\Users\smarkowitz\AppData\Local\Programs\Python\Python*\python.exe",
        "C:\Python*\python.exe",
        "C:\Program Files\Python*\python.exe",
        "C:\Program Files (x86)\Python*\python.exe"
    )
    
    $fileSystemPythons = @()
    foreach ($pathPattern in $possiblePaths) {
        $foundPaths = Get-ChildItem -Path $pathPattern -ErrorAction SilentlyContinue
        foreach ($pythonExe in $foundPaths) {
            try {
                $version = & $pythonExe.FullName --version 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $versionNum = ($version -replace 'Python ', '').Split('.')[0..1] -join '.'
                    $fileSystemPythons += [PSCustomObject]@{
                        Path = $pythonExe.FullName
                        Version = [version]$versionNum
                        VersionString = $version.Trim()
                    }
                }
            } catch {
                continue
            }
        }
    }
    
    # Prefer 3.12, then 3.11, then others from file system
    if ($fileSystemPythons.Count -gt 0) {
        $preferred = $fileSystemPythons | Where-Object { 
            $_.Version -ge [version]"3.11" -and $_.Version -lt [version]"3.14" 
        } | Sort-Object @{Expression = {
            if ($_.Version -ge [version]"3.12" -and $_.Version -lt [version]"3.13") { 1 }
            elseif ($_.Version -ge [version]"3.11" -and $_.Version -lt [version]"3.12") { 2 }
            else { 3 }
        }}, Version -Descending | Select-Object -First 1
        
        if (-not $preferred) {
            $preferred = $fileSystemPythons | Sort-Object Version -Descending | Select-Object -First 1
        }
        
        if ($preferred) {
            $pythonExe = $preferred.Path
            $pythonDir = Split-Path $pythonExe
            
            # Add Python directory and Scripts to PATH for this session
            $pythonScripts = Join-Path $pythonDir "Scripts"
            if (Test-Path $pythonScripts) {
                $env:PATH = "$pythonScripts;$pythonDir;$env:PATH"
            } else {
                $env:PATH = "$pythonDir;$env:PATH"
            }
            
            $pythonCmd = $pythonExe
            Write-Host "Found Python at: $pythonExe" -ForegroundColor Green
            Write-Host "Python version: $($preferred.VersionString)" -ForegroundColor Green
        }
    }
}

if (-not $pythonCmd) {
    Write-Error "Python not found. Please install Python 3 or update the script with your Python installation path."
    exit 1
}

# Create virtual environment if it doesn't exist
if (!(Test-Path $venvDir)) {
    Write-Host "Creating virtual environment in .venv/..." -ForegroundColor Cyan
    if ($pythonCmd -like "py *" -or $pythonCmd -eq "py") {
        # py launcher command - use as-is or add -3
        if ($pythonCmd -eq "py") {
            & $pythonCmd -3 -m venv $venvDir
        } else {
            & $pythonCmd -m venv $venvDir
        }
    } else {
        & $pythonCmd -m venv $venvDir
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create virtual environment."
        exit 1
    }
    Write-Host "Virtual environment created." -ForegroundColor Green
} else {
    Write-Host "Virtual environment already exists." -ForegroundColor Green
}

# Activate virtual environment and use its Python
if (Test-Path $venvPython) {
    $env:PATH = "$(Join-Path $venvDir 'Scripts');$env:PATH"
    $pythonCmd = $venvPython
} else {
    Write-Warning "Virtual environment Python not found at expected path. Using system Python."
}

# Install dependencies if requirements.txt exists (use venv Python if available)
if (Test-Path $requirementsFile) {
    Write-Host "Checking dependencies from requirements.txt..." -ForegroundColor Cyan
    $pipCmd = Join-Path (Split-Path $pythonCmd) "pip.exe"
    if (!(Test-Path $pipCmd)) {
        $pipCmd = "pip"
    }
    
    # Check if dependencies are installed by trying to import the first one
    $firstDep = (Get-Content $requirementsFile | Where-Object { $_ -notmatch '^\s*#' -and $_ -notmatch '^\s*$' } | Select-Object -First 1).Split('=')[0].Split('>')[0].Split('<')[0].Split('!')[0].Trim()
    
    $needsInstall = $true
    if ($firstDep) {
        try {
            $testResult = & $pythonCmd -c "import $($firstDep.Replace('-', '_'))" 2>&1
            if ($LASTEXITCODE -eq 0) {
                $needsInstall = $false
                Write-Host "Dependencies appear to be installed." -ForegroundColor Green
            }
        } catch {
            # Dependencies not installed, will install below
        }
    }
    
    if ($needsInstall) {
        Write-Host "Installing dependencies from requirements.txt..." -ForegroundColor Cyan
        
        # First, try to install with pre-built wheels only (faster, no compilation needed)
        $installSuccess = $false
        Write-Host "Attempting to install with pre-built wheels..." -ForegroundColor Cyan
        
        $pipArgs = @("-m", "pip", "install", "--only-binary", ":all:", "-r", $requirementsFile)
        if ($pythonCmd -like "py *" -or $pythonCmd -eq "py") {
            # py launcher command - use as-is or add -3
            if ($pythonCmd -eq "py") {
                $pipArgs = @("-3") + $pipArgs
                & $pythonCmd $pipArgs
            } else {
                & $pythonCmd $pipArgs
            }
        } else {
            & $pythonCmd $pipArgs
        }
        
        if ($LASTEXITCODE -eq 0) {
            $installSuccess = $true
            Write-Host "Dependencies installed successfully (using pre-built wheels)." -ForegroundColor Green
        } else {
            # If pre-built wheels aren't available, try building from source
            Write-Host "Pre-built wheels not available. Attempting to build from source..." -ForegroundColor Yellow
            Write-Host "This may require Microsoft Visual C++ Build Tools..." -ForegroundColor Yellow
            
            $pipArgs = @("-m", "pip", "install", "-r", $requirementsFile)
            if ($pythonCmd -like "py *" -or $pythonCmd -eq "py") {
                # py launcher command - use as-is or add -3
                if ($pythonCmd -eq "py") {
                    $pipArgs = @("-3") + $pipArgs
                    & $pythonCmd $pipArgs
                } else {
                    & $pythonCmd $pipArgs
                }
            } else {
                & $pythonCmd $pipArgs
            }
            
            if ($LASTEXITCODE -eq 0) {
                $installSuccess = $true
                Write-Host "Dependencies installed successfully (built from source)." -ForegroundColor Green
            } else {
                Write-Warning "Failed to install dependencies."
                Write-Warning "This may be because:"
                Write-Warning "  1. Microsoft Visual C++ 14.0+ Build Tools are required to compile dependencies"
                Write-Warning "  2. Pre-built wheels may not be available for Python 3.14 yet"
                Write-Warning ""
                Write-Warning "Solutions:"
                Write-Warning "  - Download Build Tools: https://visualstudio.microsoft.com/visual-cpp-build-tools/"
                Write-Warning "  - Or consider using Python 3.11 or 3.12 which have better wheel support"
                Write-Warning ""
                Write-Warning "Continuing anyway - the script may fail when importing dependencies..."
            }
        }
    }
} else {
    Write-Host "No requirements.txt found. Skipping dependency installation." -ForegroundColor Yellow
}

# Create wrapper functions to bypass execution policy restrictions
if ($pythonCmd -like "py *" -or $pythonCmd -eq "py") {
    # py launcher command
    if ($pythonCmd -eq "py") {
        function python { & $pythonCmd -3 $args }
        function py { & $pythonCmd -3 $args }
    } else {
        function python { & $pythonCmd $args }
        function py { & $pythonCmd $args }
    }
} else {
    function python { & $pythonCmd $args }
    function py { & $pythonCmd $args }
}

Write-Host "Using Python: $pythonCmd" -ForegroundColor Green
Write-Host "Environment ready. This applies to the current PowerShell session only." -ForegroundColor Green

