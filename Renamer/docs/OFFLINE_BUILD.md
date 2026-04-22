# Offline Build Mode — IT Deployment Guide

**For:** IT teams evaluating Spec Header Updater in environments where outbound
internet access is restricted or forbidden.
**Owner:** Solomon Markowitz (szmjuly@gmail.com)

This document describes the **offline build variant** of Spec Header Updater,
a version of the application that is guaranteed not to reach outside the
local network. It exists specifically to address the case where corporate
security policy forbids the app from calling cloud services (license
validation, telemetry, update checks).

---

## What changes in offline mode

| Capability | Networked build | Offline build |
|---|---|---|
| Opens Word docs, updates headers, converts .doc, exports PDF | ✅ Works | ✅ Works |
| Global/local file operations | ✅ Works | ✅ Works |
| License check against Firebase | ✅ Runs on startup | ❌ Skipped — user gets full access |
| Per-document usage telemetry | ✅ Logged to Firebase | ❌ Not logged |
| Auto-update probe | ✅ (when implemented) | ❌ Disabled |
| Firebase / Google Auth libraries in the bundle | Present | **Not present** |
| Outbound HTTPS traffic | Yes (Firebase) | **None** |

The offline build is byte-for-byte missing the `firebase_admin`, `pyrebase`,
and `google.auth` packages — so the executable cannot reach Firebase even if
something tried to. IT can inspect the `.exe` with any PyInstaller unpacker
and confirm those libraries are absent.

---

## How to produce an offline build

1. Open `build_config.json` in the repository root and set:

    ```json
    {
      "network_features_enabled": false,
      "include_licensing": false
    }
    ```

2. Run the build:

    ```powershell
    python build_exe.py
    ```

3. The output will be `dist/SpecHeaderUpdater-Offline.exe`. The
   `-Offline` suffix is automatic so you cannot confuse it with the
   networked build on disk.

4. The build output includes a one-line banner confirming offline mode:

    ```
    OFFLINE BUILD: Firebase and network libraries will be EXCLUDED
      - No firebase_admin / pyrebase / google.auth in the bundle
      - No license check, no telemetry, no auto-update at runtime
      - The resulting exe literally cannot reach outside the network
    ```

5. Deploy `SpecHeaderUpdater-Offline.exe`. No firewall rules, no outbound
   allowlist, no `firebase_config.json`, no service-account files are
   required anywhere on the client machine.

---

## How to test a networked build in offline mode without rebuilding

For quick IT verification without building a separate binary:

```powershell
# PowerShell
$env:SPEC_UPDATER_OFFLINE = "1"
.\SpecHeaderUpdater.exe
```

```cmd
:: cmd.exe
set SPEC_UPDATER_OFFLINE=1
SpecHeaderUpdater.exe
```

The runtime override makes any build behave exactly like an offline build:
no license check, no telemetry, no outbound traffic. The window title will
show `— Offline Build` so it's visible in screenshots.

**Caveat:** the runtime override prevents *calls* to Firebase, but the
Firebase libraries are still *present* in a networked build's binary.
Use the env-var override for "does everything else still work?" testing,
and build a true offline .exe for final IT sign-off.

---

## How to verify an offline build really is offline

Three independent checks:

### 1. Look at the window title

The title bar reads:

> **Spec Header Date & Phase Updater  —  Offline Build**

If it doesn't say "Offline Build", it isn't one.

### 2. List the bundled modules

PyInstaller's single-file builds extract to a temp directory on launch.
Start the app, then while it's running:

```powershell
Get-ChildItem $env:TEMP -Recurse -Filter "_MEI*" -Directory | ForEach-Object {
    Get-ChildItem $_.FullName -Recurse -Filter "firebase*" -ErrorAction SilentlyContinue
    Get-ChildItem $_.FullName -Recurse -Filter "google.auth*" -ErrorAction SilentlyContinue
    Get-ChildItem $_.FullName -Recurse -Filter "pyrebase*" -ErrorAction SilentlyContinue
}
```

This should return **zero** results for the offline build. The networked
build will return dozens.

### 3. Watch the network

Run the app while capturing its network activity:

```powershell
# Option A: Process Monitor (procmon) filtered to SpecHeaderUpdater.exe,
# event "TCP Connect"
# Option B: Resource Monitor > Network > Processes with Network Activity
# Option C: Fiddler or Wireshark with a host filter for *.firebaseio.com
```

An offline build will register **zero** outbound HTTPS connections regardless
of activity inside the app.

---

## What the offline build still does locally

- Reads and writes Word files you point it at
- Caches UI preferences under `%LOCALAPPDATA%\SpecHeaderUpdater\`
- Writes a persistent device ID (`device_id.txt`) purely so the dev team can
  correlate bug reports if you volunteer them. Not transmitted.

Nothing beyond that.

---

## Rolling back

To switch a machine from the offline build to the networked build, uninstall
the offline exe (delete it and the `%LOCALAPPDATA%\SpecHeaderUpdater\`
directory) and redeploy the networked exe. The two builds do not share state
in any problematic way; the networked build will re-provision its own
device ID and license state the first time it runs.

---

## Questions for your vendor review

- **Process name:** `SpecHeaderUpdater-Offline.exe`
- **Install surface:** user-level only (`%LOCALAPPDATA%\SpecHeaderUpdater\`)
- **Registry writes:** none
- **Scheduled tasks / services / auto-start:** none
- **Outbound network:** none
- **Windows APIs used:** Word COM automation (only if the user opens a legacy
  `.doc` or exports to PDF — standard Office scripting)

If anything here doesn't match what your EDR or DLP tooling observes, please
send us the report and we'll investigate.
