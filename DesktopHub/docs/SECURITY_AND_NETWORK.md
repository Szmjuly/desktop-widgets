# DesktopHub — Security & Network Reference

**Audience:** IT / security teams evaluating or troubleshooting DesktopHub deployments.
**Version covered:** 2.0+ (post-migration to Cloud-Function-minted auth tokens).
**Last updated:** 2026-04-21.

This document describes exactly what DesktopHub does on a network and on disk, so you can allowlist it in EDR / DLP / firewall / VPN-posture tooling without guessing. Everything here is deterministic and observable; if you see the app doing something not documented here, treat that as an incident.

---

## 1. One-line summary

DesktopHub is a Windows desktop productivity launcher and widget host. It phones home to a single Firebase (Google Cloud) project for license validation, update checks, role membership, and (opt-in) usage telemetry. All outbound traffic is HTTPS to Google-owned endpoints, gated by short-lived Firebase Auth ID tokens minted by a Cloud Function. No admin-SDK credentials ship with the binary.

---

## 2. Process / file identity

| Field | Value |
|---|---|
| Process name | `DesktopHub.exe` |
| Install location | `%LOCALAPPDATA%\DesktopHub\DesktopHub.exe` |
| Runtime | .NET 8, self-contained single-file, ReadyToRun |
| Size | ~180 MB (self-contained runtime included) |
| Target OS | Windows 10 1809+, Windows 11 |
| Architecture | x64 only |
| Autostart | HKCU `Software\Microsoft\Windows\CurrentVersion\Run` (user-level, no admin) |
| Start Menu entry | `%APPDATA%\Microsoft\Windows\Start Menu\Programs\DesktopHub.lnk` |
| Code signing | Not currently signed (EV cert not yet procured — known gap, see §10) |
| Update channel | Firebase-hosted with **RSA-SHA256 detached signature verification** on every downloaded build |
| SHA-256 of current release | Printed on release page at [GitHub Releases](https://github.com/Szmjuly/desktop-widgets/releases) |

Verify a shipped build matches what we published:
```powershell
Get-FileHash .\DesktopHub.exe -Algorithm SHA256
```

---

## 3. Outbound network

**All outbound traffic is HTTPS / TCP 443** to Google-owned endpoints. DesktopHub opens no raw sockets, uses no UDP, and does no multicast, mDNS, SSDP, or LAN scanning.

### Endpoints

| Host | Port | Purpose | Cadence |
|---|---|---|---|
| `us-central1-licenses-ff136.cloudfunctions.net` | 443 | `issueToken` — mint a 1-hour Firebase Auth custom token on startup. Also `pushForceUpdate` / `clearForceUpdate` (admin-tier users only). | Once on app launch; admin ops on demand |
| `identitytoolkit.googleapis.com` | 443 | Exchange the custom token for a Firebase ID token (Google's standard Identity Toolkit REST). | Once per sign-in |
| `securetoken.googleapis.com` | 443 | Refresh the ID token before its 1-hour expiry. | Every ~55 minutes while app is running |
| `licenses-ff136-default-rtdb.firebaseio.com` | 443 | Firebase Realtime Database REST. Heartbeat, license read, role lookups, update version check, opt-in telemetry. | Heartbeat every 10 min; update check every configurable interval (default 6 h); telemetry sync every 30 min |
| `github.com` / release asset CDN | 443 | Download new build during an update (URL is published in Firebase `app_versions/desktophub/download_url`). | Only when an update applies, with signature verification before install |

### Allowlist recipe (domains, wildcards OK)

```
*.cloudfunctions.net           # issueToken + admin callables
identitytoolkit.googleapis.com # custom-token exchange
securetoken.googleapis.com     # ID-token refresh
*.firebaseio.com               # RTDB REST (license, heartbeat, telemetry)
github.com                     # update downloads (release assets)
objects.githubusercontent.com  # GitHub release asset redirect target
```

No on-prem, no VPN-required, no LAN-scan endpoints.

---

## 4. What data is sent outbound

### Always sent (functional — required for the app to run)

| Field | Source | Example | Notes |
|---|---|---|---|
| `device_id` | Deterministic SHA-256 of `MachineName + MAC + Windows username`, stored locally as a GUID | `7806d23f-61e9-963c-6097-8bc887846941` | Persistent per machine; derived ID, never a raw identifier |
| `device_name` | WMI `Win32_ComputerSystem` manufacturer + model | `Dell Inc. Precision 5570` | Hardware model only, no hostname |
| `username` | Windows username, lowercased | `jsmith` | Used as a role-lookup key (`admin_users/jsmith`, `dev_users/jsmith`). Non-hashable — required for the privilege model to work. |
| `mac_address` | **Hashed.** Salted SHA-256 of raw MAC, truncated to 16 hex chars | `a1b2c3d4e5f6789a` | Raw MAC **never leaves the machine**. Hash is deterministic for duplicate-detection, not reversible. |
| `platform`, `platform_version`, `machine` | `Environment.OSVersion`, 32/64-bit | `Windows / 10.0.26200 / x64` | Standard OS metadata |
| `license_key` | Local file (`%LOCALAPPDATA%\DesktopHub\license_key.txt`). Auto-provisioned as `FREE-<8hex>-<8rand>` on first run if absent. | `FREE-A59DB35F-43HLWR0H` | |
| `last_seen`, `status` | Heartbeat timestamp, "active"/"inactive" | | |
| `app_version` | Binary version | `1.9.0` | |

### Sent ONLY with explicit user consent (usage telemetry)

Default state is **off** on a fresh install. First launch shows a Privacy dialog asking yes/no. Toggle is available any time at **Settings → Privacy**.

| Field | When |
|---|---|
| Feature usage counts (searches, hotkey presses, widget opens) | Batched, synced every ~30 min |
| Session durations | At app close |
| Anonymized performance timings | On slow operations |
| Error stack traces | When an exception escapes to the top level |

### Never sent

- Contents of searches, files, or documents
- Keystrokes (no keyloggers, no `SetWindowsHookEx(WH_KEYBOARD_LL)`)
- Clipboard contents
- Screen contents (no `BitBlt`, `PrintWindow`, or DXGI desktop duplication)
- Raw MAC address, IP address, hostname, full machine name
- Any email address, name, or personally identifiable free-form text

---

## 5. Local data / disk state

All app state lives under the user's profile — nothing system-wide beyond the install itself.

| Path | Contents |
|---|---|
| `%LOCALAPPDATA%\DesktopHub\DesktopHub.exe` | The app binary (read-only after install) |
| `%LOCALAPPDATA%\DesktopHub\settings.json` | User preferences, theme, widget layout, telemetry consent |
| `%LOCALAPPDATA%\DesktopHub\device_id.txt` | Persistent device GUID |
| `%LOCALAPPDATA%\DesktopHub\license_key.txt` | Local license key |
| `%LOCALAPPDATA%\DesktopHub\metrics.db` | SQLite of local usage events (used for the Metrics Viewer widget; only synced to Firebase if telemetry consent is granted) |
| `%LOCALAPPDATA%\DesktopHub\logs\debug.log` | Rolling debug log. Never shipped externally. |
| `%LOCALAPPDATA%\DesktopHub\telemetry-consent.json` *(planned)* | Explicit consent decision timestamp |

No writes to `%PROGRAMFILES%`, `%WINDIR%`, `HKLM`, or any other shared location. User-level install only.

---

## 6. Win32 APIs used, and why

A non-exhaustive list of user32 / kernel32 / WMI primitives the app touches. Every one has a specific, legitimate purpose.

| API | Where | Why |
|---|---|---|
| `RegisterHotKey` | `GlobalHotkey.cs` | Global keyboard shortcut to open the search overlay (e.g. Ctrl+Alt+Space). **Not** a keyboard hook — this is the per-combo registration API, single-event only, no keystroke stream. |
| `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` | `DesktopFollower.cs` | Detects when the active window changes so DesktopHub can move its overlay to the user's current virtual desktop. Event-driven; zero polling. |
| `GetForegroundWindow` | `DesktopFollower.cs` | Used in response to the above event to determine which virtual desktop the user is now on. |
| `IVirtualDesktopManager` COM interface | `VirtualDesktopManager.cs` | `MoveWindowToDesktop` / `IsWindowOnCurrentDesktop` — moves our overlay between virtual desktops. Public Microsoft API. |
| `SetWindowCompositionAttribute` / `DwmExtendFrameIntoClientArea` | `WindowBlur.cs` | Acrylic blur on the overlay windows. Standard WPF styling. |
| `WMI Win32_ComputerSystem` | `DeviceIdentifier.cs` | Reads manufacturer + model once at startup for the `device_name` field. No other WMI queries. |
| `NetworkInterface.GetAllNetworkInterfaces` | `DeviceIdentifier.cs` | Reads the first operational NIC's MAC to derive the device ID. MAC is hashed before any outbound write. |

### What the app does NOT do

No `SetWindowsHookEx(WH_KEYBOARD_LL)` / no low-level keyboard hook.
No clipboard monitoring (`AddClipboardFormatListener`, `SetClipboardViewer`).
No screen capture.
No process injection (`CreateRemoteThread`, `WriteProcessMemory`, `VirtualAllocEx`).
No process enumeration beyond our own (`Process.GetProcessesByName("DesktopHub")` for single-instance check).
No firewall or proxy configuration changes.
No registry writes outside `HKCU\...\Run\DesktopHub` (autostart) and `HKCU\Software\DesktopHub` (if we ever use it — currently we don't).
No anti-debug / anti-analysis / anti-VM checks.
No cert-pinning or TLS-validation bypass.

---

## 7. Update security

DesktopHub auto-updates via Firebase, with **cryptographic signature verification on every downloaded build**.

1. Client reads `app_versions/desktophub/download_url` from Firebase.
2. Downloads both `DesktopHub.exe` and `DesktopHub.exe.sig` (detached RSA-SHA256 signature, 3072-bit key).
3. Verifies the signature against a public key **embedded in the client at compile time** (see `assets/update-keys/current.pub.pem` in the source repo).
4. If the signature fails to verify, the download is deleted and an error is logged. The update does NOT install.
5. If it verifies, the client runs a batch script that replaces the exe and restarts.

The RSA private key used to sign releases lives only on the developer's machine and in offline backup. It is not stored on any cloud service, in Firebase, in the repo, or in any CI configuration a network attacker could reach. A compromised Firebase project **cannot** push a malicious build to installed clients — the private key is the sole ship-gate.

---

## 8. Authorization model

The client **does not ship with any admin-SDK credentials**. On first launch it authenticates via a small Cloud Function (`issueToken`) that:

1. Validates the license key against Firebase.
2. Reads `admin_users/{username}` and `dev_users/{username}` to determine the caller's **tier** (`user`, `dev`, or `admin`).
3. Mints a short-lived (1 hour) Firebase Auth custom token with `tier` as a claim.

The client exchanges that for a Firebase ID token and uses it as the `?auth=` credential on every Realtime Database REST call. Database rules (`database.rules.json` in the repo) enforce per-tier access:

- `user`: can write their own device heartbeat/telemetry; cannot read other devices; cannot write roles.
- `dev`: read across devices/metrics/events/errors, push updates; cannot modify admin_users.
- `admin`: full control, including role management.

Even if an attacker reverse-engineered the binary, they'd get at most a `user`-tier token scoped to a license that hasn't been issued to them. They'd see their own device, nothing else.

---

## 9. Removing DesktopHub cleanly

Ship `DesktopHubPurge.exe` (published at `tools/DesktopHubPurge/publish/` in our build artifacts) to any machine and run it. It:

- Kills the running `DesktopHub.exe` process
- Removes autostart registry entries (HKCU and HKLM)
- Deletes `%LOCALAPPDATA%\DesktopHub` and `%APPDATA%\DesktopHub`
- Removes Program Files install directory (admin only)
- Removes Start Menu / Desktop shortcuts
- Scrubs residual Uninstall-keys
- With `--wipe-firebase`, also removes the device record server-side

Supports `--yes` for unattended runs and `--help` for options. Exit codes: 0 clean, 1 partial failure, 2 user aborted.

---

## 10. Known gaps & roadmap

- **Code signing (Authenticode)** — not yet implemented. Windows SmartScreen may block first-run until reputation accumulates. An EV certificate is planned before external distribution.
- **Update-signing key rotation** — documented in `assets/update-keys/README.md`. Current key lifetime is ~2 years.
- **Telemetry transparency dashboard** — ability for users to see the exact events being queued / synced. Considered but not yet implemented.

---

## 11. For your EDR / DLP vendor

The historical incident that prompted this document was an EDR flagging DesktopHub 1.9 as "commodity spyware pattern" due to the combination of global hotkeys + 500ms window-enumeration polling + MAC exfiltration + unsigned binary. 2.0+ addresses this as follows:

| Old pattern (1.9 and earlier) | New behavior (2.0+) |
|---|---|
| `EnumWindows` + `GetForegroundWindow` polled every 500ms | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` event-driven, zero polling |
| Raw MAC sent to Firebase | Hashed MAC (16-char salted hex), raw MAC never leaves device |
| Admin-SDK credentials embedded in binary | No service-account credentials on the client; tokens minted server-side |
| Telemetry always on | Off by default, explicit opt-in dialog on first launch, user-controllable toggle |
| Auto-update trusts whatever Firebase points to | RSA-SHA256 signature verified against embedded public key before install |

If your EDR still flags 2.0+, please share the specific detection and we'll work with your vendor to whitelist.

---

## Contact

- **Primary:** szmjuly@gmail.com
- **Source:** https://github.com/Szmjuly/desktop-widgets
- **Issue tracker:** https://github.com/Szmjuly/desktop-widgets/issues
