# DesktopHub Admin Scripts

## Quick Start

```powershell
# Launch interactive admin console
.\scripts\admin.ps1

# Or run a specific action directly
.\scripts\admin.ps1 -Action db-dump
.\scripts\admin.ps1 -Action tags-get -ProjectNumber "2024278.01"
.\scripts\admin.ps1 -Action db-wipe-tags
```

## Master Script: `admin.ps1`

The single entry point for all admin operations. Run without arguments for an interactive menu, or pass `-Action` for direct execution.

| Action | Description |
|--------|-------------|
| `db-dump` | Show Firebase database structure |
| `db-wipe-devices` | Wipe the devices node |
| `db-wipe-tags` | Wipe project tags (Firebase + local cache) |
| `db-wipe-all` | Full reset (preserves licenses/versions/admins) |
| `tags-get` | Get decrypted tags for a project |
| `tags-list` | List all tag entries (summary) |
| `tags-decrypt` | Decrypt & dump all tags (readable) |
| `tags-set` | Set a tag value |
| `tags-delete` | Delete a tag or all tags for a project |
| `tags-export` | Export tags to CSV |
| `tags-import` | Import tags from CSV |
| `admin-list` | List admin users |
| `admin-add` | Add an admin user |
| `admin-remove` | Remove an admin user |
| `auth-cleanup` | Delete all Firebase Auth users |
| `auth-cleanup-anon` | Delete anonymous Auth users only |
| `metrics-reset` | Reset ALL local metrics |
| `version-update` | Update app version in Firebase |
| `build` | Build single-file executable |
| `build-installer` | Build installer package |
| `show-secret` | Show HMAC secret for sharing |

## Active Scripts

| Script | Purpose |
|--------|---------|
| `admin.ps1` | Master admin console (interactive menu + CLI) |
| `dump-database.ps1` | Firebase DB viewer with wipe options (-WipeDevices, -WipeTags, -WipeAll) |
| `tag-manager.ps1` | Project tag CRUD with AES encryption/decryption |
| `manage-admin.ps1` | Admin user management (add/remove/list) |
| `cleanup-auth-users.ps1` | Firebase Authentication user cleanup |
| `wipe-devices.ps1` | Device node wiper |
| `Reset-Metrics.ps1` | Local SQLite metrics database reset |
| `Update-FirebaseVersion.ps1` | Firebase app version updater (authenticated) |
| `build-single-file.ps1` | Build DesktopHub as single-file .exe |
| `build-installer.ps1` | Build full installer package |

## `_Archive/` Folder

Contains obsolete, one-time, or superseded scripts kept for reference:
- Migration scripts (completed)
- Simplified/unauthenticated version updaters (superseded)
- Legacy installer scripts (superseded)
- One-time utilities (icon conversion, dev environment setup)

## Service Account

Scripts auto-detect the Firebase service account from `secrets/firebase-license.json`.
Override with: `-ServiceAccountPath "path\to\sa.json"`

## Tag Encryption

Tag values are encrypted with AES-256-CBC using a local secret (`%LOCALAPPDATA%\DesktopHub\tag_secret.key`).
Project numbers are hashed with HMAC-SHA256 using the same secret.
Use `.\admin.ps1 -Action show-secret` to export the secret for sharing between machines.
