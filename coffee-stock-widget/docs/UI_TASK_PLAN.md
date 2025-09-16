# Coffee Stock Widget — UI Task Plan

This plan tracks UI polish and bug fixes. Each task includes goal, steps, files, and acceptance criteria.

## Tasks

1) Tray context menu modernization
- Goal: Rounded edges, drop shadow, consistent styling.
- Steps:
  - Add keyed styles `TrayMenuStyle` and `TrayMenuItemStyle` in `src/CoffeeStockWidget.UI/App.xaml`.
  - Ensure both the tray menu and reload button context menu use these keys.
- Files: `App.xaml`, `MainWindow.xaml.cs` (already references keys).
- Acceptance: Menus have rounded corners and shadow. Style applies in both tray and reload menus.

2) Expose and persist "Run Enabled" roaster list
- Goal: Let users choose which roasters are included in "Run Enabled Now".
- Steps:
  - Verify the Settings dialog checkboxes persist to `AppSettings.EnabledParsers` and update `_sources.Enabled`.
  - Clarify label text for the section.
- Files: `SettingsWindow.xaml`, `SettingsWindow.xaml.cs`, `MainWindow.xaml.cs`, `AppSettings.cs`.
- Acceptance: Toggling checkboxes changes which roasters run when selecting "Run Enabled Now".

3) Hover highlight on coffee items
- Goal: Slight highlight on hover.
- Steps:
  - Ensure `ItemsControl` item container adds a subtle background on hover.
  - Confirm pointer events reach the template border.
- Files: `MainWindow.xaml`.
- Acceptance: Hovering any coffee item visibly highlights that row.

4) Background-only blur behavior
- Goal: Blur applies to the window background only, not content.
- Steps:
  - Use OS blur-behind via `SetWindowCompositionAttribute` (already present).
  - Ensure `RootBorder.Effect` remains `null`.
- Files: `MainWindow.xaml.cs` (ApplyVisualSettings/EnableBlurBehind), `SettingsWindow.xaml` (toggles).
- Acceptance: Enabling blur leaves text/images sharp; only the glass background blurs.

5) Settings dialog layout fixes
- Goal: Prevent buttons from being pushed off; correct button heights; ensure color Apply/Reset visible; roaster list scrollable.
- Steps:
  - Wrap roaster list in a `ScrollViewer` with `MaxHeight` or place it in a star row.
  - Set `Height="28"` for Save/Cancel.
- Files: `SettingsWindow.xaml`.
- Acceptance: Save/Cancel always visible; roaster list scrolls when long; Apply/Reset buttons accessible.

6) Roaster color visibility
- Goal: Make roaster-specific color more noticeable in the list.
- Steps:
  - Widen the left accent bar in item template, and ensure highlight is perceptible.
- Files: `MainWindow.xaml`.
- Acceptance: Color strip is clearly visible and reflects chosen color.

## Notes
- Persistence model: `AppSettings.EnabledParsers` and `AppSettings.CustomParserColors` drive UI state and run behavior.
- After saving settings, `MainWindow` reloads settings, reapplies visuals, syncs enabled roasters, refreshes view, and restarts the loop.
