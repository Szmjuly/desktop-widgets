# Version 1.5.0 - Smart Project Search Expansion and Multi-Hotkey Groups

## 🎉 New Features

### Smart Project Search Widget
- Added a dedicated **Smart Project Search** widget with:
  - Queryable path search
  - Configurable file type filtering
  - Regex-based document discovery

### Smart Project Search Display Modes
- Added **Attach Mode** to embed Smart Project Search directly inside the main Search Overlay.
- Added detached window support for Smart Project Search with subfolder path handling fixes.

### Multi-Hotkey Group System
- Refactored hotkeys to support **multiple hotkey groups**.
- Each group can target a distinct set of widgets for more flexible launch/focus workflows.
- Added per-widget hotkey focus targeting behavior.

## 🐛 Fixes

### Overlay Layout and Sizing Reliability
- Improved Search Overlay resize behavior.
- Added dynamic max-height constraints and follower attachment logic for better stability.

### Smart Search UX and Path Handling
- Fixed Smart Project Search detached-window subfolder path behavior.
- Added dark-themed context menu styling for visual consistency.

## 🎨 Improvements

### Appearance Controls
- Added independent transparency controls for Smart Project Search.
- Added optional slider-linking behavior for easier coordinated adjustments.

### Internal Architecture
- Refactored SearchOverlay into clearer partial classes for hotkey/visibility and widget-window responsibilities.

### Documentation
- Consolidated documentation structure and updated internal references.

## 📌 Notes
- This release is a **minor version bump** from `1.4.1` to `1.5.0` because it introduces significant new capabilities (new widget behavior, attach/detach modes, and multi-hotkey grouping) while preserving backward compatibility.
