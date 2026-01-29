# DesktopHub - Build and Packaging Guide

This guide explains how to build and package DesktopHub for distribution.

## Quick Start

### Option 1: Single-File Executable (Easiest)

```powershell
# Build as single executable
.\scripts\build-single-file.ps1

# The executable will be in: publish\DesktopHub.UI.exe
# You can copy this single file to any Windows machine and run it directly
```

### Option 2: MSI Installer (Professional)

```powershell
# Build application and create MSI installer
.\scripts\build-installer.ps1

# The installer will be in: installer-output\DesktopHub-Setup.msi
# Users can run this MSI to install DesktopHub properly
```

## Detailed Instructions

### Prerequisites

- .NET 8.0 SDK (for building)
- PowerShell (for running scripts)
- For MSI installer: WiX Toolset v3.11+ OR Inno Setup 6.x

### Building the Application

#### Method 1: PowerShell Script (Recommended)
```powershell
# Build single-file executable
.\scripts\build-single-file.ps1
```

#### Method 2: Manual Commands
```powershell
# Clean previous build
dotnet clean src\DesktopHub.UI\DesktopHub.UI.csproj -c Release

# Build and publish as single file
dotnet publish src\DesktopHub.UI\DesktopHub.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

### Creating Installers

#### Option A: WiX Toolset (MSI Installer)
1. Install [WiX Toolset v3.11+](https://wixtoolset.org/releases/)
2. Run the build script:
   ```powershell
   .\scripts\build-installer.ps1
   ```
3. The MSI file will be created in `installer-output\`

#### Option B: Inno Setup (Recommended for most users)
1. Install [Inno Setup 6.x](https://jrsoftware.org/isinfo.php)
2. Build the application first:
   ```powershell
   .\scripts\build-single-file.ps1
   ```
3. Compile the installer:
   ```powershell
   # Using Inno Setup command line
   iscc installer\desktophub.iss
   ```
4. The installer will be created in `installer-output\`

## Distribution Options

### 1. Single-File Executable
- **Pros**: No installation required, just copy and run
- **Cons**: Larger file size (~80-100MB), no registry entries
- **Best for**: Portable use, testing, quick distribution

### 2. MSI Installer
- **Pros**: Professional installation, proper uninstall, registry entries
- **Cons**: Requires installation, more complex
- **Best for**: Enterprise deployment, proper software distribution

### 3. Inno Setup Installer
- **Pros**: Easy to create, customizable, good user experience
- **Cons**: Requires Inno Setup to build
- **Best for**: Most distribution scenarios

## File Structure After Build

```
DesktopHub/
├── publish/                    # Single-file executable
│   └── DesktopHub.UI.exe      # ~80-100MB, self-contained
├── installer-output/           # Installers
│   ├── DesktopHub-Setup.exe    # Inno Setup installer
│   └── DesktopHub.msi          # WiX MSI installer
└── scripts/                    # Build scripts
    ├── build-single-file.ps1
    └── build-installer.ps1
```

## Application Features

The built application includes:
- Project search functionality
- Widget launcher with timer widget
- System tray integration
- Global hotkey support (Ctrl+Alt+Space)
- Settings persistence
- Multi-monitor support

## Installation Notes

### Single-File Executable
1. Download the `.exe` file
2. Place it anywhere (e.g., `C:\Program Files\DesktopHub\`)
3. Run it directly
4. Optional: Create a shortcut manually

### MSI Installer
1. Download the `.msi` file
2. Double-click to run installer
3. Follow installation wizard
4. DesktopHub will be installed to Program Files
5. Shortcuts created in Start Menu and Desktop

### Inno Setup Installer
1. Download the `.exe` installer
2. Double-click to run
3. Follow installation wizard
4. Optional: Enable auto-start during installation

## Troubleshooting

### Build Issues
- Ensure .NET 8.0 SDK is installed
- Run PowerShell as Administrator if needed
- Check that all project references are available

### Installer Issues
- For WiX: Ensure WiX Toolset is in PATH
- For Inno Setup: Ensure Inno Setup is installed
- Check that the single-file executable exists before building installer

### Runtime Issues
- Ensure target machine has Windows 10/11
- Check .NET 8.0 Desktop Runtime is installed (if not self-contained)
- Verify antivirus isn't blocking the executable

## Advanced Options

### Custom Build Configuration
Edit `src\DesktopHub.UI\DesktopHub.UI.csproj` to modify:
- Target framework
- Build options
- Publishing settings
- Application metadata

### Custom Installer Configuration
Edit the installer files:
- `installer\DesktopHub.wxs` (WiX)
- `installer\desktophub.iss` (Inno Setup)

### Version Management
Update version numbers in:
- `src\DesktopHub.UI\DesktopHub.UI.csproj`
- Installer files
- `DesktopHub.sln` (if needed)

## Support

For build issues:
1. Check this guide first
2. Verify all prerequisites are installed
3. Review error messages carefully
4. Check the project's GitHub issues

For application issues:
1. Check the main README.md
2. Review the troubleshooting section
3. Report issues on the project repository
