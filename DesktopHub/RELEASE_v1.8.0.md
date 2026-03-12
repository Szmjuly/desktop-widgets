# DesktopHub v1.8.0

## Dynamic Theming, Step-by-Step Guides, Widget Grid Layout, and Remote Updates

### 🎉 New Features

#### Dynamic Theming System

- Replaced all hardcoded color values and `StaticResource` brush references with a `DynamicResource` brush system across all overlays and widgets.
- Added `ThemeBrushes.xaml` as the centralized brush dictionary — all UI colors now derive from a single source.
- Added `ThemeHelper.cs` with typed static brush and color properties for use in code-behind.
- Added `ThemeService` for runtime theme management.
- Covers: SearchOverlay, CheatSheetWidget, all overlay headers, comboboxes, scrollbars, tag carousel chips, history pills, result badges, and status bars.

#### Step-by-Step Guide System in Cheat Sheets

- Added `GuideStep` and `StepField` models to the `CheatSheet` data model to support structured procedural content.
- Steps support input/output fields, formula expressions, tips, references, icons, and per-field highlighting.
- Added three view mode tabs to the Cheat Sheet detail panel:
  - **Text / Table** — original rendered view
  - **Interactive** — step-through guide with live formula calculation
  - **Visual** — visual layout of the same guide content
- Tab set is shown automatically on sheets that have structured steps defined.
- Added steps data to initial sheets including commercial kitchen hoods and hydraulic calc sheets.

#### GEC Wire Cross-Sectional Area Reference Table

- Added a collapsible wire reference panel inside the GEC Calculator.
- Shows all supported wire sizes with their circular mil area and mm² equivalent.
- Toggled via a chevron header — collapsed by default, expands inline.

#### Non-Live Widget Grid Layout

- When "Live Layout" is off, widgets now auto-position into a stable grid arrangement instead of overlapping.
- Added `ComputeNonLivePosition` algorithm to place widgets in a left-to-right, top-to-bottom grid relative to the search overlay.
- Added `WidgetPinnedPositions` — positions are saved per widget and restored across sessions.
- Added `LastDisplayConfigFingerprint` to detect monitor configuration changes and reset pinned positions when the display layout changes.
- Added `GetAllScreensInDips` and `GetDisplayConfigFingerprint` to `ScreenHelper` for multi-monitor layout calculations.

#### Remote Forced Update System

- Admins can now remotely push an update to specific devices or all outdated devices via Firebase.
- Added `push-update.ps1` script with `list`, `push`, `push-all`, `status`, and `clear` actions.
- Added `force_update/{deviceId}` Firebase node with full status tracking: `pending`, `downloading`, `installing`, `completed`, `failed`.
- Includes retry logic (max 3 attempts) and per-device status reporting.
- App detects a forced update on startup and handles it through the existing update flow.

### 🐛 Fixes

#### View Mode Toggle Label Consistency

- Removed emoji characters from view mode toggle button labels (`Table` / `Lookup`) to prevent encoding issues and improve cross-platform reliability.
- Added `SemiBold` weight, centered alignment, and `MinWidth` to the toggle button for consistent sizing across sheets.

#### GEC Sheet Layout

- GEC sheet now correctly shows the view mode toggle (Table / Lookup switch).
- `InputPanel` is hidden for the GEC sheet since sizing is handled entirely by the calculator.
- `DesiredWidthChanged` is now invoked at the correct point in the sheet render lifecycle.

#### Tag Registry Integration

- After saving project tags, any new custom tag keys are now registered to the shared Firebase tag registry automatically.
- Local vocabulary cache is refreshed from the tag service cache after each save so new values appear immediately.

### 🎨 Improvements

#### CheatSheet Detail Panel

- Added a named `NoteTextPanel` border for targeted style control in the text/table view.
- Footer border now uses `BorderSubtleBrush` instead of a hardcoded semi-transparent value.
- Scrollviewer, note panel, and footer grid rows renumbered cleanly to accommodate the new tabs row.

#### Brush Naming Standardization

- Introduced semantic brush names used consistently throughout all XAML:
  - `HoverBrush`, `HoverMediumBrush`, `SelectedBrush`
  - `BlueBackgroundBrush`, `BlueBorderBrush`, `BlueTextBrush`
  - `GoldBackgroundBrush`, `GoldDarkBrush`
  - `OrangeBackgroundBrush`, `OrangeBrush`
  - `RedBackgroundBrush`, `RedBrush`
  - `GreenBrush`, `GreenBackgroundBrush`
  - `TextPlaceholderBrush`, `TextTertiaryBrush`
  - `FaintOverlayBrush`, `BorderSubtleBrush`, `ScrollbarThumbAltBrush`

### 📌 Notes

- This release is a minor version bump from `1.7.0` to `1.8.0`.
- The dynamic theming infrastructure introduced here is the foundation for future light/dark theme switching.
- The step-by-step guide system is the initial implementation — more sheets will gain structured steps in subsequent releases.
- The HAP Extractor is a separate companion tool added to the monorepo and is not part of the DesktopHub application.

## Assets

- `DesktopHub.exe` — single-file self-contained build
