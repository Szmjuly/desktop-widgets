# DesktopHub Cloud Functions

Server-side auth + privileged ops for DesktopHub. Runs on Firebase Cloud
Functions (Node 20, Firebase Functions v2).

## What's here

| Function | Who can call | What it does |
|---|---|---|
| `issueToken` | anyone | Mints a short-lived (1h) Firebase custom token tied to the caller's tier (`user` / `dev` / `admin`). Client then exchanges for an ID token and uses it for RTDB calls. |
| `pushForceUpdate` | `admin` only | Writes `force_update/{deviceId}`. Replaces the old client-side write. |
| `clearForceUpdate` | `admin` or `dev` | Removes `force_update/{deviceId}`. |

## One-time setup (before first deploy)

On the dev machine (not the build machine — this lives only on your workstation):

```powershell
# 1. Install the Firebase CLI globally
npm install -g firebase-tools

# 2. Log in to Google with the account that owns the licenses-ff136 project
firebase login

# 3. From the repo root, sanity-check the config
firebase projects:list
firebase use licenses-ff136

# 4. Install function dependencies
cd functions
npm install
cd ..
```

You also need the Blaze (pay-as-you-go) plan enabled on the Firebase project
before Cloud Functions will deploy. Cost at our scale is pennies per month.

Enable **Firebase Authentication** in the Firebase console
(Build → Authentication → Get started). You don't need to enable any sign-in
provider UI — custom tokens work without one. If it prompts you to pick a
provider to activate the service, enable **Anonymous** and leave it alone;
clients never use it.

## Deploy

```powershell
# Deploy just the functions
firebase deploy --only functions

# Deploy just the RTDB rules
firebase deploy --only database

# Deploy both at once
firebase deploy --only functions,database
```

After deploy, your function URLs are:

- `https://us-central1-licenses-ff136.cloudfunctions.net/issueToken`
- `https://us-central1-licenses-ff136.cloudfunctions.net/pushForceUpdate`
- `https://us-central1-licenses-ff136.cloudfunctions.net/clearForceUpdate`

## Local emulator (for changes)

```powershell
cd functions
npm run serve        # spins up functions + database emulators
```

## Viewing logs

```powershell
firebase functions:log
# or in the console:
#   https://console.cloud.google.com/logs/query?project=licenses-ff136
```

## Rollback

If something breaks after a deploy:

```powershell
# List deployed versions
gcloud functions list --regions us-central1

# Roll the rules back to the previous JSON in git
git checkout HEAD~1 -- database.rules.json
firebase deploy --only database
```

## Relationship to the client

- `FirebaseService.cs` calls `issueToken` on startup (via an HTTPS POST in the
  Firebase callable format: `{"data": {...}}` → `{"result": {...}}`).
- `DeveloperPanelWidget.AdminOps.cs` calls `pushForceUpdate` / `clearForceUpdate`
  with `Authorization: Bearer <idToken>` instead of writing to the DB directly.
- `scripts/push-update.ps1` is **unchanged** — it keeps using the
  `firebase-adminsdk-fbsvc` service-account JSON on your dev machine, which
  bypasses rules. That key never touches client binaries.

## Migration order (first rollout)

See the main plan file at `plans/we-just-ran-into-cuddly-neumann.md` — the
important part is: deploy rules + functions BEFORE shipping the new client,
but leave the old `desktophub-client-telemetry` admin role in place until
every client has picked up the new build. Then strip the role and rotate the
old key.
