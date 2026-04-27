#!/usr/bin/env node
/**
 * Admin CLI for DesktopHub tenant role management.
 *
 * Writes to /tenants/{tenantId}/{admin_users|dev_users|cheat_sheet_editors}/{user_id}
 * and maintains the /tenants/{tenantId}/user_directory entry with an AES-256-GCM
 * encrypted username. Uses firebase-admin directly (bypasses security rules) and
 * reads the per-tenant salt + encrypt key from the DPAPI-encrypted local cache
 * created by scripts/bootstrap-tenant-secrets.ps1.
 *
 * Subcommands:
 *   set-admin <user>         add user to admin_users (+ ensure directory entry)
 *   remove-admin <user>      remove from admin_users
 *   set-dev <user>           add to dev_users
 *   remove-dev <user>        remove from dev_users
 *   set-editor <user>        add to cheat_sheet_editors
 *   remove-editor <user>     remove from cheat_sheet_editors
 *   list                     list all users with their role flags (decrypted)
 *   check <user>             show role flags for one user (by raw username)
 *
 * Global flags:
 *   --tenant <id>            tenant id (default: ces)
 *
 * Prereqs (same as migrate-to-tenants.js):
 *   FIREBASE_ADMIN_KEY_PATH env var pointing to service account JSON
 *   FIREBASE_DATABASE_URL env var
 *   DPAPI cache at %LOCALAPPDATA%\DesktopHub\tenant-secrets\<tenant>.json
 */

const admin = require("firebase-admin");
const { spawnSync } = require("child_process");
const crypto = require("crypto");
const fs = require("fs");
const os = require("os");
const path = require("path");

// ─── args ──────────────────────────────────────────────────────────
const args = process.argv.slice(2);
function opt(name, fallback = null) {
  const i = args.indexOf(`--${name}`);
  if (i < 0) return fallback;
  const val = args[i + 1];
  args.splice(i, 2);
  return val;
}
const TENANT = (opt("tenant", "ces") || "ces").toLowerCase();
const YES = opt("yes");          // confirmation phrase for destructive ops
const SECTION = opt("section");   // tenant subsection name (wipe-section)
const [cmd, rawUsername] = args;

function usage(msg) {
  if (msg) console.error(`error: ${msg}`);
  console.error(`usage: admin-cli <set-admin|remove-admin|set-dev|remove-dev|
  set-editor|remove-editor|list|check> [username] [--tenant <id>]`);
  process.exit(1);
}
if (!cmd) usage();
const mutatingCmds = new Set([
  "set-admin", "remove-admin", "set-dev", "remove-dev",
  "set-editor", "remove-editor", "check",
]);
if (mutatingCmds.has(cmd) && !rawUsername) usage(`'${cmd}' requires a username`);

// ─── secrets (DPAPI) ──────────────────────────────────────────────
function readDpapiCache(tenantId) {
  const cachePath = path.join(
    process.env.LOCALAPPDATA || path.join(os.homedir(), "AppData", "Local"),
    "DesktopHub", "tenant-secrets", `${tenantId}.json`);
  if (!fs.existsSync(cachePath)) {
    console.error(`secret cache not found: ${cachePath}`);
    console.error(`  run: ./scripts/bootstrap-tenant-secrets.ps1 -TenantId ${tenantId}`);
    process.exit(1);
  }
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
const { salt, encryptKey } = readDpapiCache(TENANT);

function hmacUserId(username) {
  const n = String(username || "").trim().toLowerCase();
  return crypto.createHmac("sha256", salt).update(n).digest("hex").slice(0, 16);
}

// Mirrors Cloud Functions' tenantKeyFor(). Hashes the plaintext tenant name
// with a fixed version prefix so the DB-path segment reveals nothing about
// which customers exist. MUST stay byte-for-byte identical with the
// Cloud Functions implementation (see functions/index.js).
function tenantKeyFor(plaintext) {
  const n = String(plaintext || "").trim().toLowerCase();
  return crypto.createHash("sha256")
    .update("dh-tenant-v1:" + n)
    .digest("hex")
    .slice(0, 16);
}
function encryptUsername(username) {
  const iv = crypto.randomBytes(12);
  const c = crypto.createCipheriv("aes-256-gcm", encryptKey, iv);
  const ct = Buffer.concat([c.update(username, "utf8"), c.final()]);
  return Buffer.concat([iv, ct, c.getAuthTag()]).toString("base64");
}
function decryptUsername(b64) {
  const buf = Buffer.from(b64, "base64");
  if (buf.length < 28) return "[malformed]";
  const iv = buf.subarray(0, 12);
  const tag = buf.subarray(buf.length - 16);
  const ct = buf.subarray(12, buf.length - 16);
  const d = crypto.createDecipheriv("aes-256-gcm", encryptKey, iv);
  d.setAuthTag(tag);
  try {
    return Buffer.concat([d.update(ct), d.final()]).toString("utf8");
  } catch {
    return "[decrypt-failed]";
  }
}

// ─── firebase-admin ────────────────────────────────────────────────
const keyPath = process.env.FIREBASE_ADMIN_KEY_PATH;
const dbUrl = process.env.FIREBASE_DATABASE_URL
  || "https://licenses-ff136-default-rtdb.firebaseio.com";
if (!keyPath || !fs.existsSync(keyPath)) {
  console.error("FIREBASE_ADMIN_KEY_PATH env var not set or file missing");
  process.exit(1);
}
admin.initializeApp({
  credential: admin.credential.cert(require(path.resolve(keyPath))),
  databaseURL: dbUrl,
});
const db = admin.database();
// `TENANT` is the plaintext tenant name (what the operator types, e.g. "ces").
// `TENANT_KEY` is the DB-facing hash of that name -- every path we touch in
// RTDB uses the hash. Secret Manager lookups + DPAPI cache still key off
// the plaintext name because those layers are admin-only.
const TENANT_KEY = tenantKeyFor(TENANT);
const tRoot = `tenants/${TENANT_KEY}`;

async function ensureUserProfile(userId, username) {
  // username_ct lives on the users/{user_id} profile node -- see
  // database.rules.json for the rationale (one node per user instead of
  // parallel user_directory + users namespaces).
  await db.ref(`${tRoot}/users/${userId}`).update({
    username_ct: encryptUsername(username.toLowerCase()),
    updated_at: new Date().toISOString(),
  });
}

async function setFlag(node, username, enabled) {
  const normalized = username.toLowerCase();
  const userId = hmacUserId(normalized);
  const ref = db.ref(`${tRoot}/${node}/${userId}`);
  if (enabled) {
    await ensureUserProfile(userId, normalized);
    await ref.set(true);
    console.log(`+ ${node}: ${normalized} -> ${userId}`);
  } else {
    await ref.remove();
    console.log(`- ${node}: ${normalized} -> ${userId}`);
  }
}

async function listAll() {
  const [usersSnap, adminSnap, devSnap, editorSnap] = await Promise.all([
    db.ref(`${tRoot}/users`).get(),
    db.ref(`${tRoot}/admin_users`).get(),
    db.ref(`${tRoot}/dev_users`).get(),
    db.ref(`${tRoot}/cheat_sheet_editors`).get(),
  ]);
  const profiles = usersSnap.val() || {};
  const admins = adminSnap.val() || {};
  const devs = devSnap.val() || {};
  const editors = editorSnap.val() || {};

  const rows = Object.entries(profiles).map(([userId, entry]) => ({
    userId,
    username: entry?.username_ct ? decryptUsername(entry.username_ct) : "",
    admin: admins[userId] === true ? "Y" : "·",
    dev: devs[userId] === true ? "Y" : "·",
    editor: editors[userId] === true ? "Y" : "·",
    lastSeen: entry?.last_seen || entry?.updated_at || "",
  }));
  rows.sort((a, b) => a.username.localeCompare(b.username));

  console.log(`tenant=${TENANT}  ${rows.length} user(s)\n`);
  console.log("  USERNAME                ADMIN DEV EDITOR  USER_ID          LAST_SEEN");
  console.log("  " + "-".repeat(80));
  for (const r of rows) {
    console.log(`  ${r.username.padEnd(22)}  ${r.admin}     ${r.dev}   ${r.editor}       ${r.userId}  ${r.lastSeen}`);
  }
}

async function check(username) {
  const userId = hmacUserId(username);
  const [a, d, e] = await Promise.all([
    db.ref(`${tRoot}/admin_users/${userId}`).get(),
    db.ref(`${tRoot}/dev_users/${userId}`).get(),
    db.ref(`${tRoot}/cheat_sheet_editors/${userId}`).get(),
  ]);
  console.log(`tenant=${TENANT}  username=${username.toLowerCase()}  user_id=${userId}`);
  console.log(`  admin  : ${a.val() === true}`);
  console.log(`  dev    : ${d.val() === true}`);
  console.log(`  editor : ${e.val() === true}`);
}

// ─── dispatch ──────────────────────────────────────────────────────
(async () => {
  try {
    switch (cmd) {
      case "set-admin":     await setFlag("admin_users", rawUsername, true); break;
      case "remove-admin":  await setFlag("admin_users", rawUsername, false); break;
      case "set-dev":       await setFlag("dev_users", rawUsername, true); break;
      case "remove-dev":    await setFlag("dev_users", rawUsername, false); break;
      case "set-editor":    await setFlag("cheat_sheet_editors", rawUsername, true); break;
      case "remove-editor": await setFlag("cheat_sheet_editors", rawUsername, false); break;
      case "list":          await listAll(); break;
      case "check":         await check(rawUsername); break;
      case "wipe-all": {
        // Nuclear: delete every top-level node including every tenant.
        const root = await db.ref("/").get();
        const val = root.val() || {};
        const children = Object.keys(val);
        console.log(`would delete ${children.length} top-level nodes: ${children.join(", ")}`);
        if (YES !== "NUKE") {
          console.log("(dry-run -- pass --yes NUKE to commit)");
          break;
        }
        for (const k of children) {
          console.log(`  deleting /${k}`);
          await db.ref(`/${k}`).remove();
        }
        break;
      }
      case "wipe-non-tenant": {
        // Delete every top-level node EXCEPT /tenants.
        const root = await db.ref("/").get();
        const val = root.val() || {};
        const targets = Object.keys(val).filter(k => k !== "tenants");
        console.log(`would delete: ${targets.join(", ") || "(nothing)"}`);
        if (YES !== "WIPE") {
          console.log("(dry-run -- pass --yes WIPE to commit)");
          break;
        }
        for (const k of targets) {
          console.log(`  deleting /${k}`);
          await db.ref(`/${k}`).remove();
        }
        break;
      }
      case "wipe-tenant": {
        // Delete /tenants/{--tenant} entirely.
        const path = `tenants/${TENANT_KEY}`;
        const snap = await db.ref(path).get();
        if (!snap.exists()) { console.log(`${path} does not exist`); break; }
        const sections = Object.keys(snap.val() || {});
        console.log(`would delete ${path} (sections: ${sections.join(", ")})`);
        if (YES !== TENANT) {
          console.log(`(dry-run -- pass --yes ${TENANT} to commit)`);
          break;
        }
        await db.ref(path).remove();
        console.log(`  deleted ${path}`);
        break;
      }
      case "wipe-section": {
        // Delete /tenants/{--tenant}/{--section}.
        const ALLOWED = new Set(["admin_users", "dev_users", "cheat_sheet_editors",
          "users", "devices", "metrics", "events", "errors", "licenses"]);
        if (!SECTION || !ALLOWED.has(SECTION)) {
          usage(`wipe-section requires --section <${[...ALLOWED].join("|")}>`);
        }
        const path = `tenants/${TENANT_KEY}/${SECTION}`;
        const snap = await db.ref(path).get();
        if (!snap.exists()) { console.log(`${path} does not exist`); break; }
        const count = Object.keys(snap.val() || {}).length;
        console.log(`would delete ${path} (${count} children)`);
        if (YES !== "WIPE") {
          console.log("(dry-run -- pass --yes WIPE to commit)");
          break;
        }
        await db.ref(path).remove();
        console.log(`  deleted ${path}`);
        break;
      }
      case "hash":
        // Print just the 16-char user_id hash for <username> under the tenant.
        // Used by admin scripts (push-update, tag-manager) to stamp audit
        // fields with the hashed id instead of a raw Windows username.
        if (!rawUsername) usage("'hash' requires a username");
        console.log(hmacUserId(rawUsername));
        break;
      default: usage(`unknown command '${cmd}'`);
    }
    process.exit(0);
  } catch (e) {
    console.error(`ERROR: ${e.message}`);
    process.exit(1);
  }
})();
