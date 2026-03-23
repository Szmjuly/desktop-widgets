# Version 1.4.0 - Widget UX Enhancements, Carry-Over Reliability, and Post-Update What's New

## 🎉 New Features

### What's New Window After Updates
- Added a larger **What's New** notification window that appears after restarting into a freshly updated build.
- Shows the installed version and a short rundown of release highlights.
- Includes graceful fallback content when release notes are unavailable.

### Better Copy Options in Search + Doc Quick Open
- Added richer right-click copy actions in Search and Doc Quick Open for faster metadata copy workflows:
  - Copy project number
  - Copy project name
  - Copy project number + name
  - Context-aware path actions for file and directory results

## 🐛 Bug Fixes

### Doc Quick Open Startup Gap Fix
- Fixed a startup layout issue where widgets could retain stale "pushed-down" positions when Doc Quick Open started compact (no project loaded).
- Added startup normalization so attached widgets are pulled back up to the expected snap gap.

### Task Carry-Over Completion Fix
- Fixed carry-over behavior where completing a carried-over task did not complete its original source task.
- Completing a carried-over copy now marks the original complete and prevents repeated future carry-over.

### Toast Notification Drag/Dismiss Polish
- Improved toast swipe behavior for smoother drag-to-dismiss.
- Added better handling for multi-monitor movement and swipe fade-out finish.

## 🎨 Improvements

### Quick Launch + Frequent Projects Contrast
- Improved text and surface contrast for better readability in dark/translucent conditions.
- Updated hover and status colors for improved visual clarity.

### Live Widget Layout Stability
- Continued refinement to snap/attachment behavior and dynamic layout movement in Living Widgets Mode.

## 📌 Notes
- This release focuses on reliability and UX polish around updates, task carry-over correctness, and widget usability.
