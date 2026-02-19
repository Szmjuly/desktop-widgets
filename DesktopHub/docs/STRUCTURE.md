# DesktopHub Repository Structure

This document defines where files should live and how to keep the codebase organized.

## Top-Level Layout

```text
DesktopHub/
├── src/                 # Application source code
├── tests/               # Unit/integration tests
├── docs/                # Documentation and guides
├── scripts/             # Build/dev/release scripts
├── assets/              # Static app assets (icons/images)
├── installer/           # Installer definitions (WiX/Inno)
├── secrets/             # Local-only secrets (gitignored)
├── publish/             # Local publish output (gitignored)
└── .dotnet/             # Local SDK bootstrap (gitignored)
```

## Source Layout

```text
src/
├── DesktopHub.Core/           # Domain models + interfaces only
├── DesktopHub.Infrastructure/ # Persistence, Firebase, scanning, search implementations
└── DesktopHub.UI/             # WPF UI and app composition
```

## DesktopHub.UI Conventions

Keep only app entry files at project root:

- `App.xaml`
- `App.xaml.cs`
- `DesktopHub.UI.csproj`

Everything else should be grouped by feature folder:

```text
DesktopHub.UI/
├── Logging/           # UI/debug logging helpers
├── Notifications/     # Toast + what's-new notification windows
├── Overlays/          # SearchOverlay, TimerOverlay, Quick*Overlay, SmartProjectSearchOverlay
├── Dialogs/           # ConfirmationDialog, AlreadyRunningDialog
├── Tray/              # TrayIcon, TrayMenu
├── Settings/          # SettingsWindow and settings controls
├── Widgets/           # Widget-specific views/services
├── Services/          # UI-facing orchestration services
├── Helpers/           # Utility code (UI-specific)
├── Converters/        # XAML converters
└── Platform/          # Window extensions, blur, Win32 interop wrappers
```

## Hygiene Rules

1. Never keep `*.backup` files in source folders.
2. Keep generated outputs (`bin/`, `obj/`, logs, `publish/`) out of git.
3. Put release notes and feature docs in `docs/` (not project root).
4. Keep one class per file where practical; split large windows into partials/user controls.
5. Favor feature folders over dumping files at project root.

## Current Cleanup Phases

1. **Phase 1 (safe):** remove backup artifacts, tighten ignore rules, define conventions.
2. **Phase 2 (moves only):** move UI files into feature folders without logic changes.
3. **Phase 3 (refactor):** split oversized files (`SearchOverlay`, `SettingsWindow`) into smaller units.
4. **Phase 4 (docs):** align README/QUICKSTART/BUILD with current behavior.
