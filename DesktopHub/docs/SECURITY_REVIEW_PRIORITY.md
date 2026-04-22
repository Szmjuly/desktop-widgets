# Internal Security Review — Program Priority List

**Purpose:** Inventory of applications and tools I've authored that need formal security-team review before broader internal deployment or any external distribution. Ordered by priority (highest risk / highest exposure first). Each entry captures **what the program is**, **what it can do**, and **what specifically needs review**.

**Audience:** Company security team.
**Owner:** Solomon Markowitz (szmjuly@gmail.com).
**Last updated:** 2026-04-22.

---

## Priority ranking rationale

Higher priority = more of the following:
- Runs continuously on end-user machines (persistent attack surface)
- Handles credentials, tokens, or access to shared infrastructure
- Reaches outside the corporate network (cloud endpoints, SaaS)
- Is already deployed to >1 machine
- Previously triggered security tooling (EDR/DLP/VPN) or had an incident

| # | Program | Priority | Deployed? | Cloud-connected? | Incident history |
|---|---|---|---|---|---|
| 1 | **DesktopHub** | **P0 — Critical** | Yes (~20 machines) | Yes (Firebase / Google Cloud) | Flagged by corporate EDR, caused VPN lockout on one machine (Apr 2026) |
| 2 | **Renamer / Spec Header Updater** | **P1 — High** | Yes | Yes (shared Firebase project with DesktopHub) | None reported; committed admin service-account key in repo history |
| 3 | **HAP Extractor** | **P1 — High** | Yes | Yes (shared Firebase project) | None reported; embedded admin SA key in binary + no update signature verification |
| 4 | **Narrative Generator** | **P2 — Moderate** | Yes (internal authoring team) | No (LAN / `Q:\` file share only) | None reported |
| 5 | **HEIC Converter** | **P2 — Low** | Yes | No (fully offline) | None reported |

**Cross-program structural note:** three of the five programs (DesktopHub, Renamer, HAP Extractor) share the single Firebase project `licenses-ff136`. Any compromise of that project's credentials is a joint blast radius across all three — so the "move to company-owned GCP org" decision from the DesktopHub entry is not a DesktopHub-scoped decision; it determines the migration scope for Renamer and HAP Extractor as well.

---

## 1. DesktopHub — P0 Critical

### What it is (bullet list)

**Core identity**
- Windows 10/11 desktop application, .NET 8, single-file self-contained executable (~180 MB)
- Installs to `%LOCALAPPDATA%\DesktopHub\` (user-level, no admin required)
- Runs continuously in the system tray; also auto-starts via HKCU Run registry entry
- Currently unsigned (Authenticode code-signing cert not yet procured)

**Primary features — what the end user sees**
- Global hotkey (default `Ctrl+Alt+Space`) that opens a blurred search overlay
- Unified project search across configured network drives (P:, Q:, L:, Archive, etc.)
- Recent / frequent project launcher
- "Cheat sheets" — browsable technical references (engineering code, AV/IT wiring, etc.)
- Quick Tasks widget — local todo list with categories and priorities
- Document Quick Open widget — pinned documents with fast re-open
- Quick Launch widget — customizable file/folder launcher
- Timer widget — countdown and stopwatch
- Project Info widget — view/edit project metadata tags (voltage, HVAC, location, etc.)
- Metrics Viewer widget — local usage analytics, with optional admin multi-user dashboard
- Developer Panel widget — internal tooling for devs only; exposes raw Firebase node read/write, user role management, force-update push, license editing
- Virtual-desktop follow-along — overlay windows move with the user across Windows virtual desktops

**Backend / infrastructure**
- Single Firebase project (`licenses-ff136` on Google Cloud) hosts:
  - Firebase Realtime Database (licenses, device registry, role membership, usage events, errors, metrics, feature flags, force-update queue)
  - Firebase Cloud Functions v2 (`issueToken`, `pushForceUpdate`, `clearForceUpdate`) for privileged server-side operations
  - Firebase Authentication (custom tokens; no user-visible login flow)
- Auto-update channel: Firebase-hosted version metadata → GitHub Release asset → RSA-SHA256 signature verified locally before install

**Network behavior**
- All outbound traffic is HTTPS/TCP 443 only
- Endpoints (all Google-owned except the release CDN):
  - `us-central1-licenses-ff136.cloudfunctions.net`
  - `identitytoolkit.googleapis.com`
  - `securetoken.googleapis.com`
  - `licenses-ff136-default-rtdb.firebaseio.com`
  - `github.com` / `objects.githubusercontent.com` (release downloads only)
- Cadence: sign-in on launch (once); token refresh every 55 min; heartbeat every 10 min; update check every 6 h (configurable); telemetry every 30 min (opt-in only)
- No raw sockets, UDP, mDNS, SSDP, broadcast, multicast, LAN scanning, or on-prem endpoints
- No proxy/firewall/network-config modification

**Windows APIs used**
- `RegisterHotKey` (single-combo global shortcut; **not** a keyboard hook)
- `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` (event-driven foreground-window notifications for virtual-desktop follow)
- `GetForegroundWindow` (in response to the above event only)
- `IVirtualDesktopManager` COM interface (move windows between virtual desktops)
- `SetWindowCompositionAttribute`, `DwmExtendFrameIntoClientArea` (acrylic-blur styling)
- WMI `Win32_ComputerSystem` (one-shot read of hardware model at startup)
- `NetworkInterface.GetAllNetworkInterfaces` (one-shot read of MAC at startup — value is hashed before any outbound write)

**Data — what's stored locally**
- `%LOCALAPPDATA%\DesktopHub\settings.json` — user preferences, widget layout, telemetry consent flags
- `%LOCALAPPDATA%\DesktopHub\device_id.txt` — persistent device GUID
- `%LOCALAPPDATA%\DesktopHub\license_key.txt` — local license
- `%LOCALAPPDATA%\DesktopHub\metrics.db` — SQLite of local usage events
- `%LOCALAPPDATA%\DesktopHub\logs\debug.log` — rolling debug log (never exfiltrated)

**Data — what's sent outbound**
- Always (functional): `device_id` (hash-derived GUID), `username` (Windows username, needed as role-lookup key), `mac_address` (**salted SHA-256 hash, 16 hex chars — raw MAC never leaves device**), `device_name` (WMI manufacturer+model), `platform_version`, `license_key`, `last_seen`
- With user consent only (telemetry, off by default): feature usage counts, session durations, anonymized performance timings, error stack traces
- Explicitly never sent: search contents, file contents, keystrokes, clipboard contents, screen contents, raw MAC, IP, hostname, any free-form text or personal identifier

**Authorization model**
- Three tiers stored in Firebase: `user`, `dev`, `admin` (dev is a superset of admin)
- On every app launch, the client calls the `issueToken` Cloud Function, which reads role membership and mints a 1-hour Firebase Auth custom token with the tier as a claim
- Every subsequent Realtime Database call is gated by that token; per-tier rules in `database.rules.json` enforce what each tier can read/write
- No service-account credentials ship in the client binary

**Update security**
- Every downloaded build is RSA-SHA256 signed with a 3072-bit key
- Public key is embedded in the client at compile time (`assets/update-keys/current.pub.pem`)
- Private key lives only on the developer's machine + 1Password backup + paper backup in a safe
- Client refuses to install any update whose detached `.sig` file fails to verify against the embedded key

**Uninstallation**
- Dedicated tool (`tools/DesktopHubPurge/publish/DesktopHubPurge.exe`) provides one-command full removal: kills the process, removes registry entries, deletes all local state, removes shortcuts, scrubs Uninstall-key entries, optionally removes the device record from Firebase server-side

### What it is (detailed paragraph)

DesktopHub is a Windows 10/11 desktop productivity launcher and widget host written in C# / .NET 8 / WPF, distributed as a single-file self-contained executable that installs into the user's `%LOCALAPPDATA%` and auto-starts via a user-scope registry `Run` entry. It presents a hotkey-triggered blurred search overlay (default `Ctrl+Alt+Space`) that unifies project search across configured network drives, along with a system-tray menu and a set of pinned widgets covering quick tasks, document access, a cheat-sheet reference library, a timer, project metadata tagging, frequent-projects, customizable quick-launch targets, and, for developer-tier users, a raw Firebase developer panel. A separate backend on Google Cloud — a single Firebase project comprising a Realtime Database, three Cloud Functions, and Firebase Authentication — handles license validation, per-user role resolution (user / dev / admin), update-version distribution, and opt-in usage telemetry. All outbound traffic is HTTPS 443 to Google-owned endpoints plus the GitHub release CDN for update downloads; there is no on-prem communication, no LAN scanning, and no non-HTTPS protocol. The client holds no service-account credentials; on startup it calls a Cloud Function that mints a 1-hour Firebase Auth custom token scoped to the caller's tier, which is then used to authenticate every subsequent database call against tier-gated security rules. The previously-raw MAC address is now hashed (salted SHA-256, 16 hex chars) before any outbound write, so no personally-identifying hardware address leaves the machine. Usage telemetry is off by default and requires explicit opt-in via a first-launch dialog and a dedicated Settings → Privacy toggle; heartbeat, license, and update checks remain on regardless, as they are required for the application to function. Auto-updates are cryptographically gated: every downloaded build must carry an RSA-SHA256 detached signature that verifies against a public key embedded in the client at compile time, so a compromise of the Firebase project alone cannot ship a malicious build to installed clients. A dedicated uninstaller (`DesktopHubPurge.exe`) provides one-command full removal of all local and optionally server-side state. The application is currently unsigned (no Authenticode); an EV certificate will be acquired before any external distribution.

### What needs security-team review

- **Unsigned binary** — plan is to acquire an EV Authenticode cert before external ship; confirm this is acceptable for internal deployment in the interim, or whether to sign now.
- **Firebase project isolation** — the `licenses-ff136` Google Cloud project is on a personal account. If the security team requires a company-owned GCP project with organization-level controls, this is the single biggest structural change outstanding.
- **Network allowlisting** — verify the endpoint list in [`SECURITY_AND_NETWORK.md`](./SECURITY_AND_NETWORK.md) can be cleanly allowlisted in the corporate EDR/DLP/firewall without triggering detections.
- **Update signing key handling** — currently the RSA private key lives on my personal developer machine with backups in 1Password and on paper. Confirm whether this meets key-management policy or whether the key needs to move to a company-managed HSM.
- **Telemetry scope** — review the full list of fields enumerated under "Data — what's sent outbound" above and confirm none cross any DLP threshold.
- **Developer Panel capabilities** — dev-tier users can read across all devices and execute force-update pushes via a Cloud Function. Confirm the tier membership management process (manual add/remove in Firebase `admin_users` / `dev_users` nodes by an existing admin) meets audit requirements.
- **EDR re-test** — the application that originally triggered the corporate EDR was v1.9. Version 2.0+ has addressed the flagged patterns (raw MAC exfil removed, 500ms window-enumeration polling replaced with event-driven hook, embedded admin-SDK key removed, telemetry now opt-in). A re-test on a machine under corporate EDR policy is the last open verification before re-deployment.
- **Legal / privacy language** — first-run telemetry-consent dialog currently contains home-grown copy. Confirm whether corporate legal wording is required.

### Known open items (roadmap, not blockers for review)

- Authenticode code-signing certificate (EV preferred for SmartScreen reputation)
- Migrate Firebase project to a company-owned GCP organization if required
- Customer-facing uninstaller installer (.msi / .exe package for IT deployment tools)
- Formal SOC 2 / ISO 27001 posture documentation if external sale proceeds

---

## 2. Renamer / Spec Header Updater — P1 High

### What it is (bullet list)

**Core identity**
- Windows desktop app, Python 3.8+ / PySide6, packaged as PyInstaller single-file `.exe` (~20 MB)
- Manual install (run from extracted folder); state lives in `%LOCALAPPDATA%\SpecHeaderUpdater\`
- One-shot GUI — no tray, no auto-start, no background service
- **Currently unsigned** (no Authenticode)

**Primary features**
- Batch-updates date and project-info fields in Word document headers
- Handles `.docx` natively; converts legacy `.doc` and exports `.pdf` via optional Word COM automation
- Tiered subscription model (Free / Premium / Business) with per-device license binding

**Backend / infrastructure**
- Shares the **same Firebase project as DesktopHub** (`licenses-ff136`) — Realtime Database + Firebase Auth (anonymous)
- No Cloud Functions; client talks to RTDB REST API directly

**Network behavior**
- HTTPS 443 only, to `licenses-ff136-default-rtdb.firebaseio.com` and Firebase Auth
- License validation on startup; usage log on each document processed
- No LAN / UDP / scanning

**Windows APIs**
- `win32com.client` (Word) — optional, only invoked when `.doc` conversion or PDF export is requested
- No hooks, no WMI beyond a one-shot device-info read, no registry writes

**Local data**
- `device_id.txt`, `subscription_spec-updater.json` (cached license), `firebase_auth_cache.json` (anon session)

**Outbound data**
- Device UUID, license-key hash, app version, document count
- **Raw MAC address + machine name currently transmitted on each license check** (not hashed — same class of issue DesktopHub v1.9 was flagged for)

**Authorization / credentials**
- Public Firebase web API key embedded in build (expected design)
- **`firebase-admin-key.json` (full Firebase admin service-account private key) is present in the working directory and listed in `.gitignore` but was committed before the ignore rule — needs to be treated as exposed**
- Anonymous Firebase Auth binds sessions to device

**Update / distribution**
- Manual `.exe` redistribution; no auto-update, no signature verification

**Uninstall**
- Manual: delete the `.exe`, delete `%LOCALAPPDATA%\SpecHeaderUpdater\`

### What it is (detailed paragraph)

Renamer (user-facing name: *Spec Header Updater*) is a Windows desktop utility written in Python 3 with a PySide6 GUI, packaged as a PyInstaller single-file executable and distributed manually. Its purpose is to batch-update the dates and project-identification fields embedded in the headers of Microsoft Word specification documents; it reads and writes `.docx` natively, and optionally invokes Word via COM automation to handle legacy `.doc` inputs and to export `.pdf` outputs. It is a one-shot GUI — launched, used, and closed on demand, with no background service, no tray presence, and no auto-start. A tiered subscription model (Free / Premium / Business) is enforced by a lightweight license client that talks to the same Firebase project as DesktopHub (`licenses-ff136`): on startup the client performs an anonymous Firebase Auth, reads its license record over the Realtime Database REST API, caches the result locally under `%LOCALAPPDATA%\SpecHeaderUpdater\`, and logs a usage event on each document processed. Outbound traffic is HTTPS 443 only, to Google-owned Firebase endpoints — there is no LAN activity, no non-HTTPS protocol, and no telemetry beyond license-and-usage enforcement. The application is currently unsigned, has no auto-update channel (new versions are redistributed manually), and uninstalls by deleting the executable and local state directory. Two items make this P1 rather than P2: the raw MAC address is still transmitted on each license check (same issue DesktopHub v1.9 was EDR-flagged for and has since fixed), and a Firebase admin service-account private key (`firebase-admin-key.json`) was committed to the repository before being added to `.gitignore` — it must be treated as exposed and rotated.

### What needs security-team review

- **Exposed Firebase admin key** — `firebase-admin-key.json` in the repo history grants full admin on the shared `licenses-ff136` project. This key must be rotated, and shared-project blast radius with DesktopHub + HAPExtractor assessed.
- **Raw MAC in outbound payload** — same class of issue that triggered EDR on DesktopHub v1.9. Recommend applying the DesktopHub v2.0 fix (salted SHA-256, 16-hex-char hash) here before re-deployment.
- **Shared Firebase project with DesktopHub + HAPExtractor** — if DesktopHub is required to migrate to a company-owned GCP org, Renamer and HAPExtractor move with it.
- **Unsigned binary** — same decision as DesktopHub: sign now for internal, or wait for EV cert.
- **Word COM surface** — confirm invoking Word via COM on documents from arbitrary network drives is acceptable under current DLP posture.

### Known open items

- Auto-update channel (currently none; every release is hand-distributed)
- Move embedded public Firebase config out of source tree into external config
- Installer / uninstaller packaging

---

## 3. HAP Extractor — P1 High

### What it is (bullet list)

**Core identity**
- Windows desktop app, .NET 8 / C#, self-contained single-file `.exe` (~100+ MB) with `PublishReadyToRun=true`
- Manual install — downloaded from GitHub releases and run in place
- Long-running interactive GUI with periodic background checks (not one-shot)
- Currently unsigned

**Primary features**
- Parses HVAC thermal-analysis PDF reports (Carrier HAP: *Heat & Air-load Program*)
  - Zone Sizing Summary and Space Design Load Summary PDFs (separate or combined)
- Structured extraction via `PdfPig`; merged into filterable grid (by room / system)
- Export to `.xlsx` via `ClosedXML`
- Display + export only; no calculation

**Backend / infrastructure**
- Shares the **same Firebase project as DesktopHub and Renamer** (`licenses-ff136`)
  - Reads update metadata from `app_versions/hapextractor`
  - Reads admin-pushed forced-update instructions from `force_update/{deviceId}`
  - Writes device registration, heartbeat, error logs, update-check events

**Network behavior**
- HTTPS 443 only
- Endpoints: `licenses-ff136-default-rtdb.firebaseio.com`; GitHub-release URLs for update payloads
- Cadence: device-registration on launch; heartbeat every 10 min; update check on demand + every 30 min
- User-Agent: `HAPExtractor-AutoUpdater/1.0`
- No LAN / UDP / scanning

**Windows APIs**
- `OpenFileDialog` for user-selected PDFs (read-only)
- `System.Management` (WMI) — device manufacturer + model (device-fingerprint)
- `System.Net.NetworkInformation` — **MAC address enumeration (raw MAC currently transmitted)**
- `Environment` — username, machine name, processor count, architecture
- Runs as standard user, no UAC elevation

**Local data**
- `%LOCALAPPDATA%\HAPExtractor\logs\hapextractor_*.log` (daily)
- `%LOCALAPPDATA%\HAPExtractor\device_id.txt`
- `%LOCALAPPDATA%\HAPExtractor\firebase-config.json` (optional external override)
- No user PDFs retained after processing

**Outbound data**
- Device ID, username, machine name, **raw MAC**, platform/OS/arch, app version, launch/close timestamps, last-seen
- **Full exception detail** (type + message + stack trace + context) — no PII redaction pass
- No PDF content

**Authorization / credentials**
- **Firebase service-account private key (`firebase-adminsdk-ftaw@licenses-ff136.iam.gserviceaccount.com`) embedded in the single-file executable** (Release builds)
- Any user running the binary inherits the service account's read-write scope on the shared Firebase project
- No end-user authentication

**Update / distribution**
- Distribution: GitHub releases (unsigned)
- Auto-update: downloads from URL read out of Firebase, stages in `%TEMP%`, swaps executable via a batch script, silent 10-second restart
- **No signature verification** on the downloaded executable
- Forced-update channel via `force_update/{deviceId}` — an admin with Firebase write access can push an arbitrary download URL to any device

**Uninstall**
- Delete the `.exe`; local data under `%LOCALAPPDATA%\HAPExtractor\` persists until manually removed
- No installer and no registry entries

### What it is (detailed paragraph)

HAP Extractor is a Windows desktop utility written in C# on .NET 8, packaged as a self-contained single-file executable and distributed through GitHub releases. Its user-facing job is narrow: it ingests PDF reports produced by Carrier's HAP (Heat & Air-load Program) HVAC-design tool — specifically the Zone Sizing Summary and the Space Design Load Summary, either as two PDFs or as one combined document — parses them structurally via `PdfPig`, merges the two views into a single filterable grid, and exports the combined result to `.xlsx` through `ClosedXML`. It performs no calculation; it is a display-and-export convenience over a proprietary report format. The backend is the same Firebase project (`licenses-ff136`) shared with DesktopHub and Renamer: the client registers the device on launch, heartbeats every ten minutes, reports exceptions as they occur, and polls `app_versions/hapextractor` plus a per-device `force_update/{deviceId}` node for update instructions. Auto-update downloads the target executable from a URL read out of Firebase, stages it in `%TEMP%`, and swaps the running binary via a batch script. Two structural items push this to P1: the Firebase service-account private key is embedded in the single-file executable (trivially extractable via decompilation), and the downloaded update executable is run without cryptographic signature verification — so whoever controls the Firebase `force_update` node controls code execution on every installed client. The raw MAC address is also still transmitted on heartbeat (the same issue already fixed in DesktopHub v2.0). The binary is unsigned, has no installer, and uninstalls by manual deletion.

### What needs security-team review

- **Embedded Firebase service-account private key** — extractable from the single-file `.exe`. Must be replaced with a tokenization model (e.g., DesktopHub's `issueToken` Cloud Function + custom-token pattern) before further deployment. Also implies key rotation on the shared project.
- **No update-signature verification** — apply the DesktopHub RSA-SHA256 detached-signature scheme before further auto-updates are allowed. Until then, the forced-update path is a remote-code-execution primitive for anyone with Firebase write access.
- **Raw MAC in heartbeat** — apply the DesktopHub v2.0 salted-SHA-256 hash.
- **Unredacted exception telemetry** — stack traces and context strings may contain file paths / usernames / project names; confirm DLP posture or add a redaction pass.
- **Shared Firebase project** — same blast-radius concern as Renamer.
- **Unsigned binary** — same Authenticode decision.
- **Forced-update UX** — 10-second silent restart gives the user essentially no window to cancel; confirm this is acceptable.

### Known open items

- Move to tokenized auth (match DesktopHub's `issueToken` pattern)
- Add RSA-SHA256 signature verification to the updater
- Hash MAC before send; add redaction pass over exception payloads
- Code signing

---

## 4. Narrative Generator — P2 Moderate

### What it is (bullet list)

**Core identity**
- Windows desktop app, Python 3 / PyQt6, PyInstaller single-file `.exe` (~66 MB)
- Source lives under `C:\Users\smarkowitz\repos\CES-Dev\NarrativeApp\V_2.0\`
- One-shot GUI — no tray, no auto-start, no background activity
- Currently unsigned

**Primary features**
- Generates revision memoranda (narratives) for MEP architectural projects
- Scans `Q:\_Proj-##` directories to extract project metadata
- Filters revision sets by discipline (Electrical, Mechanical, Plumbing, Fire Protection)
- Extracts sheet names from PDFs via `PyPDF2`
- Renders `.docx` via `docxtpl` against `Q:\Templates\Narratives\Narrative_Template.docx`
- Archives previous revisions under `_ARCHIVE\REVN\` with date stamps before overwriting

**Backend / cloud**
- **None.** No cloud services, no LLM APIs (OpenAI / Anthropic / Azure), no telemetry
- Pure local + LAN file-share operation

**Network behavior**
- No outbound internet traffic
- Reads from and writes to the `Q:` network drive only (enterprise SMB share)

**Windows APIs**
- `os.startfile()` to open the generated `.docx` in Word
- `QSettings` → HKCU registry (organization key: `"ces"`) for theme preference
- `os.walk()` against `Q:\` for project discovery — no permission checks beyond OS ACLs

**Local data**
- Theme QSS files copied to `%USERPROFILE%\Documents\NarrativeApp\Themes\` on first frozen run
- Theme preference in HKCU registry
- No license file, no auth tokens, no device ID

**Outbound data**
- None

**Authorization / credentials**
- None — relies entirely on Windows ACLs on `Q:\`
- No hardcoded secrets

**Update / distribution**
- Manual `.exe` redistribution; no auto-update, no version beacon

**Uninstall**
- Delete the `.exe`; optionally remove `%USERPROFILE%\Documents\NarrativeApp\` and the HKCU `ces` registry key

### What it is (detailed paragraph)

The Narrative Generator is an internal Windows desktop tool written in Python 3 with a PyQt6 GUI, packaged as a PyInstaller single-file executable and distributed manually. It exists to automate the production of MEP revision-memorandum documents: it scans the `Q:\_Proj-##` project directory tree on the enterprise file share, pulls project metadata and the list of revised drawing sheets out of the relevant PDFs, and renders a Word narrative from a `docxtpl` template kept alongside the projects on `Q:\`. A revision is written to `Q:\[Project]\Narratives\RevN.docx` and the previous `RevN.docx`, if present, is moved to a dated archive path first. The application is strictly offline with respect to the internet — no cloud backend, no LLM calls, no telemetry, no auto-update, no license check — its only external dependency is SMB access to `Q:\`, governed by the existing Windows ACLs on that share. It holds no credentials, hardcoded or otherwise; the only persistent client-side state is a theme preference stored in HKCU via Qt's `QSettings` under the organization key `"ces"` and a copy of the theme stylesheets under `%USERPROFILE%\Documents\NarrativeApp\`. Uninstall is a manual delete of the executable and, optionally, those two artifacts. From a security standpoint this is the smallest attack surface of the five programs — no network surface beyond what the user's logon already grants on the corporate share — and the review is primarily a verification that the tool's `Q:\` read/write scope aligns with the authoring team's existing permissions on that share.

### What needs security-team review

- **`Q:\` scope** — confirm that read access to every `_Proj-##` directory and write access into each project's `Narratives\` subfolder is consistent with the authoring team's intended ACL model.
- **Hardcoded template path** — `Q:\Templates\Narratives\Narrative_Template.docx` has no fallback. Confirm this is acceptable, or whether an env-var/config indirection is required.
- **PDF parsing surface** — `PyPDF2` processes arbitrary PDFs from the share. Confirm current `PyPDF2` pinned version has no known parser CVEs.
- **Unsigned binary** — same Authenticode decision as the other four.

### Known open items

- Code signing
- Optional config indirection for template path

---

## 5. HEIC Converter — P2 Low

### What it is (bullet list)

**Core identity**
- Windows desktop app, .NET 8 / C# (WPF + CLI front ends, `net8.0-windows`), self-contained single-file `.exe`
- Manual install, drop-in executable
- One-shot (CLI) or GUI (drag-and-drop) — no tray, no background activity
- Currently unsigned

**Primary features**
- Converts Apple HEIC / HEIF images to `.jpg` or `.png`
- Batch mode with optional recursive folder traversal, configurable JPEG quality (1–100), overwrite toggle
- Uses the OS-native **Windows Imaging Component (WIC)** HEIF codec — requires Microsoft HEIF Image Extensions from the Store

**Backend / cloud**
- **None.** Fully offline.

**Network behavior**
- No outbound traffic of any kind

**Windows APIs**
- WIC for decode; standard file I/O for encode
- No shell-extension registration, no context-menu install, no registry writes beyond optional Qt/WPF preferences
- No hooks, no WMI, no COM automation

**Local data**
- `%LOCALAPPDATA%\HeicConvert\settings.json` — UI preferences only (output format, quality, flags)
- No device ID, no license, no tokens

**Outbound data**
- None

**Authorization / credentials**
- None

**Update / distribution**
- Manual redistribution; no auto-update, no version beacon

**Uninstall**
- Delete the `.exe`; optionally remove `%LOCALAPPDATA%\HeicConvert\`

### What it is (detailed paragraph)

HEIC Converter is a small .NET 8 Windows utility (WPF GUI plus a CLI entry point) that converts Apple HEIC/HEIF image files into JPEG or PNG, with batch support, optional recursive folder traversal, configurable JPEG quality, and an overwrite toggle. It is built as a self-contained single-file executable and distributed manually. It is strictly offline: no cloud services, no telemetry, no auto-update, no license check, no credentials — the only local state it keeps is a small `settings.json` of UI preferences under `%LOCALAPPDATA%\HeicConvert\`. Decoding is delegated to the Windows Imaging Component's HEIF codec (which requires the Microsoft HEIF Image Extensions to be installed from the Microsoft Store); the app therefore carries no bundled third-party HEIC parser — which is a deliberate risk-reduction choice, since most HEIC-parsing CVEs over the last few years have been in libraries like `libheif` and `ImageMagick`. The primary security-review items are the absence of Authenticode code-signing (consistent with the other four programs and handled under the same decision) and the fact that the app's decode security posture is effectively "inherits whatever the OS HEIF codec ships" — which is Microsoft's to patch. There is no network surface and no persistence surface beyond the preferences file.

### What needs security-team review

- **Unsigned binary** — same Authenticode decision as the other four.
- **WIC HEIF codec dependency** — confirm the managed-fleet policy keeps the Microsoft HEIF Image Extensions on a current patch level (this is the sole image-parsing attack surface).
- **Input-path scope** — confirm batch/recursive mode's read-and-write pattern against user-selected roots is consistent with DLP expectations.

### Known open items

- Code signing
- Optional: Explorer context-menu integration (would add a shell-extension review item if pursued)

---

## Filling in a new entry (template)

Copy this block and fill in each field. Every program gets:

1. **Priority** (P0 / P1 / P2) with a one-sentence rationale
2. **Bullet list** covering at minimum: core identity, primary features, backend, network behavior, Windows/OS APIs used, local data, outbound data, authorization model, update/distribution mechanism, uninstall story
3. **Detailed paragraph** — 200–400 words, prose, reads like an elevator pitch to someone outside the codebase
4. **What needs review** — specific open questions or gaps the security team should answer
5. **Known open items** — roadmap work that's planned but not blocking this review
