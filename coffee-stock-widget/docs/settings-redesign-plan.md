# Settings UI Redesign Plan

## Goals
- Provide a cleaner, more discoverable layout by grouping related settings into tabs.
- Reduce visual clutter via search filtering, section captions, and consistent spacing.
- Maintain existing functionality while improving readability and navigation.

## Proposed Structure
- **General**
  - Polling interval, run at login, AI enable toggle
  - Appearance (transparency, blur, acrylic, accent tint)
  - Retention/new badge options
- **Notifications**
  - Fetch notes toggle + limits
  - AI summarization parameters (model, endpoint, caps, temperature, top-p, etc.)
- **Roasters**
  - Enabled roaster list
  - Per-roaster colors editor
- **Advanced**
  - Max per source/events
  - Database maintenance actions

## Key UI Elements
- Smaller window (approx 520x420) centered, still using rounded translucent chrome.
- `TabControl` on the left with icons/text.
- Top search box filtering visible settings within active tab.
- Each tab content laid out with `UniformGrid`/`StackPanel` combos and descriptive labels.
- Primary call-to-action buttons pinned at bottom right.

## Implementation Steps
1. Update `SettingsWindow.xaml` layout to use a 2-column grid: left nav (`TabControl`), right search + content area.
2. Create `TabItem`s for General, Notifications, Roasters, Advanced with section headers.
3. Introduce search box bound to a helper that toggles visibility of labeled rows by matching text.
4. Refactor code-behind to initialize new controls and adapt load/save logic to new names.
5. Ensure existing bindings, event handlers, and validation adapt to reorganized controls.
