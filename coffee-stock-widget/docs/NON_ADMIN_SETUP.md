# Non-admin Setup

You can develop and run this project without admin rights by using a local .NET SDK inside the repo.

## One-time steps
1) Open PowerShell in the repo root: `coffee-stock-widget/`
2) Allow the current session to run the setup script (does not change machine policy):
   ```powershell
   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
   ```
3) Run the developer environment script (downloads a local .NET SDK to `.dotnet/` if needed and updates PATH for this session):
   ```powershell
   .\scripts\dev-env.ps1
   ```

If the script says to dot-source it, run:
```powershell
. .\scripts\dev-env.ps1
```

## What the script does
- Downloads `dotnet-install.ps1` from Microsoft (to `.dotnet/`) if missing
- Installs the .NET SDK specified in `global.json` into `.dotnet/`
- Adds `.dotnet/` to `PATH` for the current session
- Defines a `dotnet` function that points to the local SDK

No admin privileges are required. All files are placed under this repository.

## Each new terminal session
- Run the script again to prepare the environment:
  ```powershell
  Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
  .\scripts\dev-env.ps1
  ```

## Optional: Node and Python (examples)
If you need Node or Python without admin rights, you can mirror this pattern:

```powershell
# Node portable example (adjust path and version)
$nodePath = "C:\Users\<you>\node\node-v22.19.0-win-x64"
$env:PATH = $env:PATH + ";" + $nodePath
function node { & "$nodePath\node.exe" $args }
function npm  { & "$nodePath\npm.cmd" $args }

# Python virtualenv example (no admin)
$venv = ".venv"
if (!(Test-Path $venv)) { python -m venv $venv }
$env:PATH = (Resolve-Path "$venv\Scripts").Path + ";" + $env:PATH
pip --version
```

## Build and run
After the environment is set up:
```powershell
# Once projects are scaffolded
 dotnet --info
 dotnet build
 dotnet test
```

## Uninstall / Clean
- Delete the `.dotnet/` folder to remove the local SDK.
- Nothing is installed system-wide.
