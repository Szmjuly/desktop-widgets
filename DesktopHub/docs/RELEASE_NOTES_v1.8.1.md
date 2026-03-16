# Version 1.8.1 - Dynamic Cheat Sheet Backend, Feeder Schedule Overhaul, and Remote Editing

## 🎉 New Features

### Dynamic Cheat Sheet Backend (Firebase-Backed)

- Migrated cheat sheet data from hardcoded C# defaults to a Firebase RTDB-backed dynamic system.
- Added `ICheatSheetDataService` interface and `CheatSheetDataService` implementation with full AES-256 encryption on Firebase, plaintext local JSON cache for fast search.
- On first launch with Firebase, all hardcoded defaults are seeded to Firebase automatically.
- Subsequent launches load from local cache instantly, then background-sync from Firebase.
- **5-minute version polling**: lightweight check of `cheat_sheet_data/meta/version` — only triggers full sync when remote data actually changed.
- **Unlimited edit history**: every save creates a timestamped snapshot in `cheat_sheet_data/history/{sheetId}/` recording who edited, when, what changed, and the full previous state (encrypted).
- **Q-Drive backup**: auto-exports plaintext JSON to `Q:\_Resources\Programs\DesktopHub\cheatsheets_backup.json` on every save for disaster recovery.
- All existing search, lookup, scoring, and rendering logic unchanged — operates on the same in-memory data store.

### Role-Based Cheat Sheet Editing

- Added **Cheat Sheet Editor** role — independent from admin. Edit access = admin OR editor.
- Added `IsCheatSheetEditorAsync` to `IFirebaseService` — checks `cheat_sheet_editors/{username}` then falls back to `admin_users/{username}`.
- Firebase node: `cheat_sheet_editors/{windows_username}: true`.
- Added `scripts/manage-cheatsheet-editors.ps1` — add/remove/list editors (mirrors `manage-admin.ps1`).

### Inline Sheet Editor UI

- **"+ Add Sheet"** button in sheet list footer (visible only for editors/admins).
- **Edit button** (pencil icon) in sheet detail header (visible only for editors/admins).
- Full inline editor panel with:
  - **Metadata fields**: Title, Subtitle, Description, Tags, Discipline, Sheet Type
  - **Column editor**: Header, Unit, IsInput/IsOutput checkboxes per column, add column button
  - **Row editor**: inline cell editing grid with add row button
  - **Save / Cancel** buttons
  - **Disable** button (editors) — soft-hides sheets from non-editors
  - **Delete** button (admins only) — hard-deletes with confirmation dialog
- Live refresh: when another user edits a sheet, the `DataUpdated` event propagates through to the widget and refreshes the sheet list automatically.

### Comprehensive Feeder Schedule Tables

- Replaced the two outdated feeder schedule sheets with four system-specific feeder schedules:
  - **1Ø 2-Wire + Ground** (`feeder-1ph-2w`) — 23 rows, 15A–400A
  - **1Ø 3-Wire + Ground** (`feeder-1ph-3w`) — 23 rows, 15A–400A (120/240V)
  - **3Ø 3-Wire + Ground** (`feeder-3ph-3w`) — 35 rows, 15A–3000A (delta)
  - **3Ø 4-Wire + Ground** (`feeder-3ph-4w`) — 35 rows, 15A–3000A (wye)
- Each sheet uses `CompactLookup` layout with cascading dropdowns for Circuit Symbol and OCPD.
- Data includes parallel conductor sets up to 3000A with proper notation for each system type.
- Rich tag sets for searchability (e.g., "delta", "wye", "2W", "4W", "120/240").

## 🎨 Improvements

### CheatSheetService Refactored

- `CheatSheetService` now accepts an optional `ICheatSheetDataService` parameter — when provided, delegates all data loading to Firebase-backed service.
- Parameterless constructor preserved as legacy fallback for offline/no-Firebase scenarios.
- `CreateDefaultData()` made `internal static` for access by the data service seeding logic.
- All hardcoded default provider files (`ElectricalDefaults.cs`, etc.) retained as seed/fallback — not deleted.

### CheatSheetOverlay Wiring

- `CheatSheetOverlay` and `CheatSheetWidget` constructors now accept optional `ICheatSheetDataService` and `IFirebaseService` for editor functionality.
- `SearchOverlay.Initialization` creates `CheatSheetDataService` when Firebase is available and passes it through the full chain.

## 📌 Notes

- This release is a patch version bump from `1.8.0` to `1.8.1`.
- The Firebase cheat sheet data structure uses AES-256-CBC encryption (same `TagValueEncryptor` pattern as project tags) — hardware-accelerated, <1ms per sheet.
- The `enabled` flag on sheets is stored unencrypted to allow server-side filtering without decryption.
- History viewer UI (revert to previous version) is planned for a future release.
- To grant a user editor access: `.\scripts\manage-cheatsheet-editors.ps1 add <username>`

## Assets

- `DesktopHub.exe` — single-file self-contained build
