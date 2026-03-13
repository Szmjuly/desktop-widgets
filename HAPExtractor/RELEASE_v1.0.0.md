# HAPExtractor v1.0.0

## Initial Release

### 🎉 New Features

#### Dual PDF Extraction Workflow

- Load a **Zone Sizing Summary** PDF and a **Space Design Load Summary** PDF.
- Extract data from both reports and combine them into a unified results view.
- Designed for a fast desktop workflow with no external installation required.

#### Interactive Results UI

- View combined data in multiple modes:
  - **All** — full grid of all matched spaces
  - **Systems** — grouped by air system
  - **Zones** — focused single-zone inspection view
- Built-in search/filtering across room names and systems.
- Inline copy-to-clipboard actions for key output sections.

#### Excel Export

- Export combined space/component load data directly to `.xlsx`.
- Output is formatted for downstream review and team handoff.

#### Firebase Live Update Integration

- Added Firebase Realtime Database integration for version checks and telemetry.
- App reads update info from `app_versions/hapextractor`.
- Added in-app **Check for Updates** button in the main window.
- Supports downloading and replacing `HAPExtractor.exe` automatically during upgrade.

#### Remote Forced Update Support

- Admins can push a forced update to a specific device or all outdated HAPExtractor devices.
- Uses the shared `force_update/{deviceId}` node with `app_id = "hapextractor"`.
- Supports update status tracking: `pending`, `downloading`, `installing`, `completed`, `failed`.

#### Device Tracking & Error Logging

- Registers device/app presence in Firebase under `devices/{deviceId}/apps/hapextractor`.
- Logs launch, close, update-check, and error telemetry to app-scoped Firebase nodes.
- Uses the shared DesktopHub Firebase project and shared service-account credentials.

### 🛠️ Infrastructure

- Added `HAPExtractor.Infrastructure` project for Firebase/auth/update functionality.
- Added build script for single-file self-contained publishing.
- Added release version script to publish version metadata to Firebase.
- Linked `HAPExtractor/secrets/` to the shared DesktopHub secrets folder.

### 📦 Assets

- `HAPExtractor.exe` — single-file self-contained .NET 8 build

### 🚀 Publish Command

```powershell
$env:PATH = "C:\dotnet;$env:PATH"; dotnet publish src\HAPExtractor.UI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -o "src\HAPExtractor.UI\bin\Release\net8.0-windows\win-x64\publish-v1.0.0"
```

### 🔗 Expected Release Download URL

```text
https://github.com/Szmjuly/desktop-widgets/releases/download/v1.0.0/HAPExtractor.exe
```

### 📌 Notes

- This is the first HAPExtractor release.
- Update metadata should be pushed to Firebase using `scripts/update-version.ps1` before or alongside the GitHub release.
- HAPExtractor uses the same Firebase backend as DesktopHub but is isolated by `app_id`.
