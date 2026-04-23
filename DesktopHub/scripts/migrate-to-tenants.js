#!/usr/bin/env node
/**
 * One-shot migration: flat RTDB layout -> /tenants/{tenantId}/ layout.
 *
 * Moves every user-scoped and license path under /tenants/{tenantId}/ and
 * rekeys raw Windows usernames to HMAC user_ids using the tenant salt from
 * Secret Manager. Populates /tenants/{tenantId}/user_directory/{user_id}
 * with AES-256-GCM-encrypted usernames using the tenant encrypt key.
 *
 * Run BEFORE deploying the new database rules. After this runs successfully
 * and clients + scripts have been updated, run `firebase deploy --only
 * database` to lock out the legacy roots.
 *
 * Usage:
 *   node scripts/migrate-to-tenants.js --tenant internal --dry-run
 *   node scripts/migrate-to-tenants.js --tenant internal
 *   node scripts/migrate-to-tenants.js --tenant internal --delete-legacy
 *
 * The --delete-legacy flag removes the old flat roots after copying. Omit it
 * the first time so you can verify /tenants/{tenantId}/ looks right before
 * destroying the source data.
 *
 * Prereqs:
 *   - FIREBASE_ADMIN_KEY_PATH env var pointing to a service account JSON
 *   - FIREBASE_DATABASE_URL env var (e.g. https://licenses-ff136-default-rtdb.firebaseio.com)
 *   - `gcloud` on PATH, authenticated as a principal with secretmanager.versions.access
 *   - Secrets tenant-salt-{tenantId} and tenant-encrypt-{tenantId} already
 *     provisioned (see bootstrap-tenant-secrets.ps1)
 */

const admin = require("firebase-admin");
const { spawnSync } = require("child_process");
const crypto = require("crypto");
const fs = require("fs");
const os = require("os");
const path = require("path");

// ─── args ──────────────────────────────────────────────────────────
const args = process.argv.slice(2);
function flag(name) { return args.includes(`--${name}`); }
function opt(name, fallback = null) {
  const i = args.indexOf(`--${name}`);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : fallback;
}
const TENANT = opt("tenant", "internal");
const DRY = flag("dry-run");
const DELETE_LEGACY = flag("delete-legacy");

if (!/^[a-z0-9-]+$/.test(TENANT)) {
  console.error(`bad tenant id '${TENANT}'`); process.exit(1);
}

// ─── secrets (DPAPI-encrypted local cache from bootstrap-tenant-secrets.ps1) ───
function readDpapiCache(tenantId) {
  const cachePath = path.join(
    process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local"),
    "DesktopHub", "tenant-secrets", `${tenantId}.json`);
  if (!fs.existsSync(cachePath)) {
    console.error(`secret cache not found: ${cachePath}`);
    console.error(`  run: ./scripts/bootstrap-tenant-secrets.ps1 -TenantId ${tenantId}`);
    process.exit(1);
  }

  // Use PowerShell to unprotect via DPAPI (same user scope as bootstrap).
  const script = `
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$b = [IO.File]::ReadAllBytes('${cachePath.replace(/\\/g, "\\\\").replace(/'/g, "''")}')
$u = [Security.Cryptography.ProtectedData]::Unprotect($b, $null,
  [Security.Cryptography.DataProtectionScope]::CurrentUser)
[Console]::Out.Write([Text.Encoding]::UTF8.GetString($u))
`;
  const r = spawnSync("powershell",
    ["-NoProfile", "-NonInteractive", "-Command", script],
    { encoding: "utf8" });
  if (r.status !== 0) {
    console.error(`DPAPI unprotect failed: ${r.stderr}`);
    process.exit(1);
  }
  const parsed = JSON.parse(r.stdout);
  return {
    salt: Buffer.from(parsed.salt_b64, "base64"),
    encryptKey: Buffer.from(parsed.encrypt_key_b64, "base64"),
  };
}

const { salt: saltBuf, encryptKey } = readDpapiCache(TENANT);
if (saltBuf.length < 16) {
  console.error(`tenant-salt-${TENANT} too short (${saltBuf.length} bytes)`);
  process.exit(1);
}
if (encryptKey.length !== 32) {
  console.error(`tenant-encrypt-${TENANT} must be 32 bytes, got ${encryptKey.length}`);
  process.exit(1);
}

function hmacUserId(username) {
  const normalized = String(username).trim().toLowerCase();
  return crypto.createHmac("sha256", saltBuf).update(normalized).digest("hex").slice(0, 16);
}

function encryptUsername(username) {
  const iv = crypto.randomBytes(12);
  const cipher = crypto.createCipheriv("aes-256-gcm", encryptKey, iv);
  const ct = Buffer.concat([cipher.update(String(username), "utf8"), cipher.final()]);
  const tag = cipher.getAuthTag();
  return Buffer.concat([iv, ct, tag]).toString("base64");
}

// ─── firebase-admin ────────────────────────────────────────────────
const keyPath = process.env.FIREBASE_ADMIN_KEY_PATH;
const dbUrl = process.env.FIREBASE_DATABASE_URL
  || "https://licenses-ff136-default-rtdb.firebaseio.com";
if (!keyPath || !fs.existsSync(keyPath)) {
  console.error("FIREBASE_ADMIN_KEY_PATH not set or file missing");
  process.exit(1);
}
admin.initializeApp({
  credential: admin.credential.cert(require(path.resolve(keyPath))),
  databaseURL: dbUrl,
});
const db = admin.database();

// ─── helpers ───────────────────────────────────────────────────────
const tRoot = `tenants/${TENANT}`;
const plan = [];

function queue(op, label) {
  plan.push({ op, label });
}

async function apply() {
  for (const { op, label } of plan) {
    console.log(DRY ? `[dry] ${label}` : `[apply] ${label}`);
    if (!DRY) await op();
  }
}

// ─── migrations ────────────────────────────────────────────────────
async function migrateRoles() {
  const adminSnap = await db.ref("admin_users").get();
  const devSnap = await db.ref("dev_users").get();

  const usernames = new Set([
    ...Object.keys(adminSnap.val() || {}),
    ...Object.keys(devSnap.val() || {}),
  ]);

  const adminUsers = adminSnap.val() || {};
  const devUsers = devSnap.val() || {};

  for (const rawUsername of usernames) {
    const username = rawUsername.toLowerCase();
    const userId = hmacUserId(username);
    const isAdmin = adminUsers[rawUsername] === true;
    const isDev = devUsers[rawUsername] === true;

    if (isAdmin) {
      queue(
        () => db.ref(`${tRoot}/admin_users/${userId}`).set(true),
        `admin ${rawUsername} -> ${userId}`,
      );
    }
    if (isDev) {
      queue(
        () => db.ref(`${tRoot}/dev_users/${userId}`).set(true),
        `dev ${rawUsername} -> ${userId}`,
      );
    }
    queue(
      () => db.ref(`${tRoot}/users/${userId}`).update({
        username_ct: encryptUsername(username),
        migrated_at: new Date().toISOString(),
      }),
      `users profile ${rawUsername} -> ${userId}`,
    );
  }
}

async function migrateUsers() {
  const snap = await db.ref("users").get();
  const users = snap.val() || {};
  for (const [rawUsername, payload] of Object.entries(users)) {
    const userId = hmacUserId(rawUsername);
    const { username, ...rest } = payload || {}; // drop raw username field
    queue(
      () => db.ref(`${tRoot}/users/${userId}`).set({
        ...rest,
        user_id: userId,
      }),
      `users/${rawUsername} -> ${tRoot}/users/${userId}`,
    );
  }
}

async function migrateDevices() {
  const snap = await db.ref("devices").get();
  const devices = snap.val() || {};
  for (const [deviceId, payload] of Object.entries(devices)) {
    const raw = (payload && payload.username) ? String(payload.username) : "";
    const userId = raw ? hmacUserId(raw) : "unknown";
    const { username, ...rest } = payload || {};
    queue(
      () => db.ref(`${tRoot}/devices/${deviceId}`).set({
        ...rest,
        user_id: userId,
      }),
      `devices/${deviceId} (user ${raw || "?"} -> ${userId})`,
    );
  }
}

async function migrateMetrics() {
  const snap = await db.ref("metrics").get();
  const metrics = snap.val() || {};
  for (const [deviceId, payload] of Object.entries(metrics)) {
    // metrics rows may have user_name fields -- strip them, replace with user_id
    const cleaned = { ...payload };
    if (cleaned.user_name) {
      const raw = String(cleaned.user_name);
      cleaned.user_id = hmacUserId(raw);
      delete cleaned.user_name;
    }
    queue(
      () => db.ref(`${tRoot}/metrics/${deviceId}`).set(cleaned),
      `metrics/${deviceId}`,
    );
  }
}

async function migrateEventsOrErrors(rootName) {
  const snap = await db.ref(rootName).get();
  const appsNode = snap.val() || {};
  for (const [appId, months] of Object.entries(appsNode)) {
    for (const [month, entries] of Object.entries(months || {})) {
      // Rekey any entries carrying a username subfield. The event keys
      // themselves (push ids) stay the same.
      const rewritten = {};
      for (const [entryId, entry] of Object.entries(entries || {})) {
        const copy = { ...entry };
        if (copy.username) {
          copy.user_id = hmacUserId(String(copy.username));
          delete copy.username;
        }
        rewritten[entryId] = copy;
      }
      queue(
        () => db.ref(`${tRoot}/${rootName}/${appId}/${month}`).set(rewritten),
        `${rootName}/${appId}/${month} (${Object.keys(rewritten).length} entries)`,
      );
    }
  }
}

async function migrateLicenses() {
  const pairs = [
    { src: "licenses", appId: "desktophub" },
    { src: "spec_updater_licenses", appId: "spec-updater" },
  ];
  for (const { src, appId } of pairs) {
    const snap = await db.ref(src).get();
    const licenses = snap.val() || {};
    for (const [licenseKey, payload] of Object.entries(licenses)) {
      const enriched = {
        ...payload,
        app_id: appId,
        tenant_id: TENANT,
      };
      queue(
        () => db.ref(`${tRoot}/licenses/${appId}/${licenseKey}`).set(enriched),
        `${src}/${licenseKey} -> ${tRoot}/licenses/${appId}/${licenseKey}`,
      );
    }
  }
}

async function deleteLegacy() {
  if (!DELETE_LEGACY) {
    console.log("(skipping legacy delete; pass --delete-legacy after verifying)");
    return;
  }
  for (const p of ["admin_users", "dev_users", "users", "devices", "metrics",
                   "events", "errors", "licenses", "spec_updater_licenses"]) {
    queue(
      () => db.ref(p).remove(),
      `DELETE legacy /${p}`,
    );
  }
}

// ─── drive ─────────────────────────────────────────────────────────
(async () => {
  console.log(`migration: tenant='${TENANT}' dry=${DRY} deleteLegacy=${DELETE_LEGACY}`);
  await migrateRoles();
  await migrateUsers();
  await migrateDevices();
  await migrateMetrics();
  await migrateEventsOrErrors("events");
  await migrateEventsOrErrors("errors");
  await migrateLicenses();
  await deleteLegacy();
  await apply();
  console.log(DRY ? "\n(dry run -- nothing written)" : "\nmigration complete.");
  process.exit(0);
})().catch(e => { console.error(e); process.exit(1); });
