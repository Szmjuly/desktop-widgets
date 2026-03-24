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

## Run

```powershell
dotnet run -- --input "C:\Photos\HEIC" --output-dir "C:\Photos\Export" --format jpg --quality 90 --recursive
```

Single-file conversion:

```powershell
dotnet run -- --input "C:\Photos\a.heic" --output-dir "C:\Photos\Export" --format png
```

No arguments mode prompts interactively for missing values.
