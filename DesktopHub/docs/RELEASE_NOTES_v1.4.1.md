# Patch Release 1.4.1 - What's New Reliability and Settings Cleanup

## 🐛 Fixes

### What's New Notification Reliability
- Fixed post-update **What's New** display by adding a version-change fallback path.
- The app now tries this order on startup:
  1. Show pending payload saved during update install (with release notes)
  2. If no payload exists, show What's New once when app version changed
- Added persisted "last shown version" tracking so users are notified after update without duplicate popups.

### Settings UI Cleanup
- Removed Frequent Projects tab transparency controls.
- Removed Quick Launch tab transparency controls.
- Transparency for these widgets is now managed only in the **Appearance** tab.

## 🎨 Quality Improvements
- Reduced settings duplication and potential confusion by consolidating transparency management into one location.

## 📌 Notes
- This is a patch release focused on update-notification reliability and settings consistency.
