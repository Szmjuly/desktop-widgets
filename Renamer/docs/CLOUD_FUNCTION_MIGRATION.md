# Renamer Cloud Function Migration — 2026-04-23

This document captures the one-time migration from Renamer's old Firebase
client (pyrebase + firebase-admin + anonymous Firebase Auth) to the
Cloud-Function-minted-token pattern that DesktopHub uses. It's intended as
the log of what changed, why, and the manual cleanup steps that go along
with it.

## What changed, at a glance

| Thing | Before | After |
|---|---|---|
| License validation | Client-side against `licenses/` flat RTDB path | `issueToken` Cloud Function with `appId="spec-updater"` |
| Client auth | Anonymous Firebase Auth via pyrebase (public API key + anon account per device) | Short-lived Firebase Auth custom token minted by the Cloud Function, carrying `tier` + `app_id` claims |
| RTDB access | Client-side SDK, full client authority under anon account | REST calls with `?auth=<idToken>`; rules gate by token claims |
| Firebase libs in the exe | `firebase-admin` + `pyrebase` + `google.auth` | None -- just `urllib` from stdlib |
| Raw MAC sent outbound | Yes -- in `device_fingerprint.mac_address` | Hashed (salted SHA-256, 16 hex chars) -- raw MAC never leaves the machine |
| License root path in RTDB | `licenses/{licenseKey}` (shared with DesktopHub) | `spec_updater_licenses/{licenseKey}` (app-scoped) |
| Device records path in RTDB | `devices/{deviceId}` (shared with DesktopHub) | `spec_updater_devices/{deviceId}` (app-scoped) |

## Why

1. **Admin-key exposure** — the old design loaded
   `firebase-admin-key.json` as a dev fallback. That key was committed
   historically and has been rotated. Removing the admin SDK entirely
   means future rotations can't accidentally re-leak.
2. **Per-app isolation** — database rules now gate `spec_updater_licenses/*`
   on `auth.token.app_id === 'spec-updater'`. A DesktopHub token cannot
   touch Renamer data and vice versa. Role-based overrides (`dev`, `admin`)
   still cross-cut as before.
3. **PII/DLP** — the raw MAC never leaves the machine. Same fix DesktopHub
   v2.0 applied.
4. **Anonymous-auth cleanup** — each old install created a unique Firebase
   Auth anonymous user. Those accumulate indefinitely. The new model uses
   deterministic uids (`su_<username>`) so we never create unbounded
   anonymous accounts.

## One-time server-side cleanup steps (you run these)

Since every existing deployment has been uninstalled and there's no data to
migrate, we can wipe the Renamer footprint in the shared Firebase project
cleanly.

### 1. Deploy the updated Cloud Function and rules

From the DesktopHub repo root:

```powershell
cd C:\Users\smarkowitz\repos\desktop-widgets\DesktopHub
firebase deploy --only functions,database
```

Watch for:
- `functions[issueToken(us-central1)] Successful update operation.`
- `database: rules for database licenses-ff136-default-rtdb released successfully`

### 2. Verify from PowerShell

```powershell
$body = @{ data = @{
    appId = 'spec-updater'
    licenseKey = 'FREE-TEST-RENAMER'
    username = 'smarkowitz'
    deviceId = 'smoke-test-renamer'
} } | ConvertTo-Json

$resp = Invoke-RestMethod -Method Post `
    -Uri 'https://us-central1-licenses-ff136.cloudfunctions.net/issueToken' `
    -ContentType 'application/json' `
    -Body $body

$resp.result | Select-Object tier, appId, uid, expiresIn | Format-List
```

Expected:
```
tier      : dev
appId     : spec-updater
uid       : su_smarkowitz
expiresIn : 3600
```

Decode the minted JWT to confirm the `app_id` claim made it in:

```powershell
$parts = $resp.result.token.Split('.')
$payloadB64 = $parts[1].PadRight(($parts[1].Length + 3) -band -4, '=').Replace('-','+').Replace('_','/')
[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payloadB64)) | ConvertFrom-Json `
    | Select-Object -ExpandProperty claims
```

You should see `app_id=spec-updater` in the claims object.

### 3. Clean up the smoke-test records

```powershell
# Delete the auto-provisioned test license
# (Firebase Console → Realtime Database → spec_updater_licenses/FREE-TEST-RENAMER → Delete)
```

### 4. Clean up the OLD Renamer anonymous-auth users

The old client path created a Firebase Auth anonymous user per device on
first launch. Those accumulate indefinitely and are no longer needed.

1. Firebase Console → **Authentication → Users**
2. Filter / sort by provider = **Anonymous** if that option is available
3. Select and delete. Anonymous accounts never come back to the service
   once deleted (they have no credentials to re-auth with). This is safe.

If the Authentication users list has hundreds of anonymous accounts and
you'd rather script it, the Admin SDK has `auth().listUsers()` and
`auth().deleteUsers([uids])` -- write a small Node.js or Python script
that runs on your dev machine, authenticated with your Jan-2026 admin-SDK
JSON. One-shot script, not something that ships anywhere.

### 5. Clean up stale RTDB records from the old shared paths

If any Renamer data ended up in the DesktopHub-shared paths (`users/`,
`devices/`, `licenses/`) that isn't needed anymore:

1. Firebase Console → **Realtime Database**
2. For each Renamer-era device, delete the corresponding entries
3. Watch out: don't delete DesktopHub devices. Renamer devices can be
   identified by their `apps/spec-updater` subtree.

Since the user count is small (~15, all uninstalled) it's fastest to just
scan by eye and delete what doesn't belong.

## New RTDB layout, for reference

```
/licenses/                     <- DesktopHub licenses (unchanged)
  {licenseKey}/

/spec_updater_licenses/        <- NEW: Renamer licenses
  {licenseKey}/
    license_key
    app_id: "spec-updater"
    plan: "free" | "premium" | "business"
    status: "active" | "expired" | ...
    usage/{deviceId}/
      last_processed_at
      used_this_month

/devices/                      <- DesktopHub devices (unchanged)
  {deviceId}/

/spec_updater_devices/         <- NEW: Renamer devices
  {deviceId}/
    device_name
    username
    mac_hash                   <- hashed, not raw
    platform, platform_version, machine
    last_seen
    status
    license_key
    app_id: "spec-updater"

/admin_users/                  <- Shared across apps (unchanged)
/dev_users/                    <- Shared across apps (unchanged)
```

## Client-side summary

The Renamer client now:
- Pulls in no Firebase SDK packages (stdlib `urllib` only)
- Signs in on launch via `src.firebase_auth.FirebaseAuth`
- Stores a deterministic 32-hex device id in `%LOCALAPPDATA%\SpecHeaderUpdater\device_id.txt`
- Sends MAC as a hashed value (never raw)
- Honors `SPEC_UPDATER_OFFLINE=1` as a runtime master switch and
  `network_features_enabled: false` in `build_config.json` as a build-time
  master switch (both skip sign-in entirely and run on a permissive dummy
  SubscriptionManager)

## If something breaks after deploy

- **401s from `issueToken`**: check that Cloud Run invoker policy still
  includes `allUsers` (it was granted during the DesktopHub migration).
- **`unknown appId` errors**: the request is missing `appId` in the body.
  Confirm the client is on the new build.
- **Rule-denied on `spec_updater_licenses/*`**: the `app_id` claim is
  missing from the token. Happens if the Cloud Function wasn't redeployed
  before the client upgraded. Redeploy functions first.
