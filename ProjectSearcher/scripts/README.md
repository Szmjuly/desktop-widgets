# Development Scripts

## dev-env.ps1

Bootstraps a local .NET SDK installation for development without requiring admin privileges.

### What it does:
1. Downloads the .NET SDK installer script from Microsoft
2. Installs .NET SDK to `./.dotnet` directory (local to this project)
3. Reads the desired SDK version from `global.json`
4. Adds the local SDK to PATH for the current PowerShell session
5. Creates a `dotnet` wrapper function

### Usage:

The `run` script in the project root automatically calls this. You can also run it manually:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\dev-env.ps1
```

### Benefits:
- **No admin required** - Installs to user directory
- **Project-isolated** - Each project can have its own SDK version
- **Version-locked** - Uses exact version from `global.json`
- **Clean** - `.dotnet` folder is gitignored

### First Run:
The first time you run this, it will:
1. Download `dotnet-install.ps1` (~50KB)
2. Download and install .NET 8 SDK (~200MB)
3. This takes 2-5 minutes depending on internet speed

### Subsequent Runs:
After the first install, the script:
1. Checks if the correct SDK version is already installed
2. If yes, just adds it to PATH (instant)
3. If no, downloads and installs the correct version

### Troubleshooting:

**Script won't run:**
```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

**Wrong SDK version:**
Delete `.dotnet` folder and run again:
```powershell
Remove-Item -Recurse -Force .\.dotnet
.\scripts\dev-env.ps1
```

**Internet connection issues:**
The script downloads from `https://dot.net/v1/dotnet-install.ps1`. Ensure you have internet access and no firewall blocking Microsoft domains.
