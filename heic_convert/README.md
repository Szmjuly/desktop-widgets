# HEIC Convert CLI

Core conversion workflow for HEIC/HEIF images, designed so the same conversion engine can later be reused by a desktop UI.

## What it does

- Accepts either a single file or a folder as input.
- Converts `.heic` / `.heif` to **`jpg`** or **`png`** (via **WPF / WIC** — no extra NuGet packages).
- Lets you control quality (`1-100`) for JPEG.
- Supports recursive folder scanning.
- Preserves folder structure when converting a directory.

## Why no extra NuGet packages?

Some environments only expose a limited NuGet feed (you hit **NU1102** for `Microsoft.Windows.SDK.NET`). This project uses **`UseWPF`** + `System.Windows.Media.Imaging` so restore works with **just the .NET SDK** — no third-party or Windows SDK NuGet packages.

**WebP** output is not included here (WPF has no built-in WebP *encoder*). Add WinRT/`Microsoft.Windows.SDK.NET` later if your feed can reach it.

## Quality behavior

- **JPEG**: `1-100` maps to `JpegBitmapEncoder.QualityLevel` (higher = larger file, fewer artifacts).
- **PNG**: effectively lossless; quality has minimal effect.

## Requirements

1. **Windows** + **.NET 8 SDK** (or `.\scripts\dev-env.ps1` for a local SDK)
2. **HEIF Image Extension** from the Microsoft Store (you already have this — needed so WIC can decode HEIC).

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\dev-env.ps1
```

## Desktop app

From the `heic_convert` folder:

```powershell
dotnet run --project HeicConvert.App
```

Drag and drop HEIC/HEIF files or folders onto the drop zone (or use **Add files…** / **Add folder…**), set the output folder and options in the **Settings** panel, then **Convert**. Settings are saved under `%LocalAppData%\HeicConvert\settings.json`.

### Single-file publish — **desktop UI** (self-contained `.exe`)

This builds **`HeicConvert.exe`**, the **WPF app** (window with drag-and-drop). It is **not** the console CLI.

**Note:** `dotnet publish` on **`HeicConvert.sln`** (or without `--project`) also produces **`HeicConvert.Cli.exe`**. For **only the UI**, use the script below or **`--project HeicConvert.App`**.

Produces one **x64** executable that includes the .NET runtime (no separate runtime install on other PCs). First run may unpack to a temp cache; size is typically on the order of tens of MB.

From the `heic_convert` folder:

```powershell
.\scripts\publish-app.ps1
```

Output: **`heic_convert\publish-ui\HeicConvert.exe`**

Manual equivalent (UI only):

```powershell
dotnet publish .\HeicConvert.App\HeicConvert.App.csproj -c Release -f net8.0-windows -p:PublishProfile=Win64-SingleFile -o publish-ui
```

### Single-file publish — CLI only (optional)

```powershell
dotnet publish .\HeicConvert.Cli.csproj -c Release -f net8.0-windows -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
  -o publish-cli
```

Output: **`HeicConvert.Cli.exe`** (console).

## Run (CLI)

```powershell
dotnet run --project HeicConvert.Cli -- --input "C:\Photos\HEIC" --output-dir "C:\Photos\Export" --format jpg --quality 90 --recursive
```

Single-file conversion:

```powershell
dotnet run --project HeicConvert.Cli -- --input "C:\Photos\a.heic" --output-dir "C:\Photos\Export" --format png
```

Build everything:

```powershell
dotnet build HeicConvert.sln
```

No arguments mode prompts interactively for missing values.
