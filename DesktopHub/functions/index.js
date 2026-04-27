/**
 * DesktopHub Cloud Functions
 *
 * Server-side only, runs with Firebase Admin SDK privileges. Clients never
 * embed the admin key. Role resolution, token minting, and privileged writes
 * all happen here; clients trust the custom claims on their ID tokens and
 * database rules enforce per-tier / per-tenant access.
 *
 * Multi-tenant model (2026-04 refactor):
 *   - Every token carries `tenant_id` (default "ces") and a `user_id`
 *     that is HMAC-SHA256(tenant_salt, lowercase(username))[:16].
 *   - Roles live under /tenants/{tenantId}/admin_users/{user_id} and
 *     /tenants/{tenantId}/dev_users/{user_id}. The database only ever sees
 *     the hashed user_id -- raw Windows usernames never hit RTDB.
 *   - Per-tenant HMAC salts are stored in Secret Manager as
 *     `tenant-salt-{tenantId}`. Bootstrapped on first use.
 */

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const { defineSecret } = require("firebase-functions/params");
const { logger } = require("firebase-functions/v2");
const admin = require("firebase-admin");
const crypto = require("crypto");

admin.initializeApp();
const db = admin.database();
const auth = admin.auth();

const REGION = "us-central1";
const TOKEN_TTL_SECONDS = 3600;
const USER_ID_HEX_LEN = 16; // 64 bits -- ample for per-tenant collision safety

// ─────────────────────────────────────────────────────────────
//  Tenant salts (Secret Manager)
// ─────────────────────────────────────────────────────────────
//
// Each tenant has its own HMAC salt so that the same Windows username in two
// different tenants hashes to different user_ids. Salts are ~32 bytes of
// randomness stored in Secret Manager under names like `tenant-salt-internal`.
//
// Functions that derive user_ids declare the specific secrets they need via
// the `secrets` option below. The `internal` tenant is wired up now; when we
// onboard an external tenant, add its secret name to TENANT_SALT_SECRETS and
// list it in the function's `secrets` array.

// Firebase CLI requires UPPER_SNAKE_CASE secret names. Runtime name in Secret
// Manager mirrors the `defineSecret` argument here. Each tenant id listed as
// a key here must have its pair of secrets provisioned before deploy (see
// scripts/bootstrap-tenant-secrets.ps1).
const TENANT_SALT_SECRETS = {
  "ces": defineSecret("TENANT_SALT_CES"),
};

// Per-tenant AES-256-GCM key for the encrypted username field stored on each
// user's profile node. RTDB sees ciphertext only; Cloud Functions decrypt on
// admin-only listTenantUsers calls.
const TENANT_ENCRYPT_SECRETS = {
  "ces": defineSecret("TENANT_ENCRYPT_CES"),
};

function getTenantSalt(tenantId) {
  const secret = TENANT_SALT_SECRETS[tenantId];
  if (!secret) {
    throw new HttpsError("invalid-argument",
      `unknown tenant '${tenantId}' (no salt configured)`);
  }
  // Stored as base64 text (Firebase CLI mangles raw binary on upload).
  const b64 = secret.value();
  if (!b64) {
    throw new HttpsError("failed-precondition",
      `tenant '${tenantId}' salt not provisioned in Secret Manager`);
  }
  const salt = Buffer.from(b64, "base64");
  if (salt.length < 16) {
    throw new HttpsError("failed-precondition",
      `tenant '${tenantId}' salt must decode to >=16 bytes (got ${salt.length})`);
  }
  return salt;
}

function getTenantEncryptKey(tenantId) {
  const secret = TENANT_ENCRYPT_SECRETS[tenantId];
  if (!secret) {
    throw new HttpsError("invalid-argument",
      `unknown tenant '${tenantId}' (no encrypt key configured)`);
  }
  // Stored as base64 of 32 raw bytes in Secret Manager.
  const b64 = secret.value();
  if (!b64) {
    throw new HttpsError("failed-precondition",
      `tenant '${tenantId}' encrypt key not provisioned`);
  }
  const key = Buffer.from(b64, "base64");
  if (key.length !== 32) {
    throw new HttpsError("failed-precondition",
      `tenant '${tenantId}' encrypt key must be 32 bytes (got ${key.length})`);
  }
  return key;
}

function hmacUserId(tenantName, username) {
  const salt = getTenantSalt(tenantName);
  const normalized = String(username || "").trim().toLowerCase();
  if (!normalized) {
    throw new HttpsError("invalid-argument", "username required");
  }
  const mac = crypto.createHmac("sha256", salt).update(normalized).digest("hex");
  return mac.slice(0, USER_ID_HEX_LEN);
}

// Deterministic, non-secret hash of the plaintext tenant name. The DB uses
// this hash as the path segment ({tenants/{tenantKey}/...}) so a Firebase
// console snapshot doesn't leak which tenants exist. The plaintext name is
// only ever seen in:
//   - the issueToken request body (client -> server, in flight)
//   - Secret Manager secret names (TENANT_SALT_CES, etc -- admin-only)
// The hash is stable across deploys (no secrets involved) but salted with
// a version prefix so we can rotate the scheme later without renaming
// every existing path.
const TENANT_KEY_PREFIX = "dh-tenant-v1:";
const TENANT_KEY_HEX_LEN = 16;

function tenantKeyFor(plaintext) {
  const n = String(plaintext || "").trim().toLowerCase();
  if (!n) throw new HttpsError("invalid-argument", "tenant name required");
  return crypto.createHash("sha256")
    .update(TENANT_KEY_PREFIX + n)
    .digest("hex")
    .slice(0, TENANT_KEY_HEX_LEN);
}

// AES-256-GCM. Output format: base64(iv[12] || ciphertext || tag[16]).
function encryptUsername(tenantId, plaintext) {
  const key = getTenantEncryptKey(tenantId);
  const iv = crypto.randomBytes(12);
  const cipher = crypto.createCipheriv("aes-256-gcm", key, iv);
  const ct = Buffer.concat([
    cipher.update(String(plaintext), "utf8"),
    cipher.final(),
  ]);
  const tag = cipher.getAuthTag();
  return Buffer.concat([iv, ct, tag]).toString("base64");
}

function decryptUsername(tenantId, b64) {
  const key = getTenantEncryptKey(tenantId);
  const buf = Buffer.from(b64, "base64");
  if (buf.length < 12 + 16) {
    throw new HttpsError("internal", "directory ciphertext malformed");
  }
  const iv = buf.subarray(0, 12);
  const tag = buf.subarray(buf.length - 16);
  const ct = buf.subarray(12, buf.length - 16);
  const decipher = crypto.createDecipheriv("aes-256-gcm", key, iv);
  decipher.setAuthTag(tag);
  return Buffer.concat([decipher.update(ct), decipher.final()]).toString("utf8");
}

const ALL_TENANT_SALTS = Object.values(TENANT_SALT_SECRETS);
const ALL_TENANT_SECRETS = [
  ...Object.values(TENANT_SALT_SECRETS),
  ...Object.values(TENANT_ENCRYPT_SECRETS),
];

// Reverse map hashed tenant_id -> plaintext name. Callers that only have
// the claim (auth.token.tenant_id = hash) use this to find the right
// Secret Manager entries for decryption. Built at cold start.
const TENANT_HASH_TO_NAME = Object.fromEntries(
  Object.keys(TENANT_SALT_SECRETS).map(name => [tenantKeyFor(name), name])
);

function tenantNameFromHash(hash) {
  const name = TENANT_HASH_TO_NAME[hash];
  if (!name) {
    throw new HttpsError("permission-denied",
      `unknown tenant hash '${hash}'`);
  }
  return name;
}

// Resolve the caller's tenant from whatever value sits in their token's
// tenant_id claim. Modern tokens carry the hashed key; older tokens issued
// before the hashing change carry the plaintext name. Returning both forms
// here lets the rest of the function reach for the right one (paths use
// `key`, Secret Manager lookups use `name`).
function resolveCallerTenant(claimValue) {
  if (!claimValue) {
    throw new HttpsError("permission-denied", "no tenant on token");
  }
  if (TENANT_HASH_TO_NAME[claimValue]) {
    return { name: TENANT_HASH_TO_NAME[claimValue], key: claimValue };
  }
  if (ALLOWED_TENANT_IDS.has(claimValue)) {
    return { name: claimValue, key: tenantKeyFor(claimValue) };
  }
  throw new HttpsError("permission-denied",
    `unknown caller tenant '${claimValue}' -- sign in again`);
}

// ─────────────────────────────────────────────────────────────
//  App / tenant config
// ─────────────────────────────────────────────────────────────

const ALLOWED_APP_IDS = new Set(["desktophub", "spec-updater"]);
const ALLOWED_TENANT_IDS = new Set(Object.keys(TENANT_SALT_SECRETS));

const UID_PREFIX = {
  "desktophub": "dh_",
  "spec-updater": "su_",
};

// Tenant-scoped license path: /tenants/{tenantId}/licenses/{appId}/{licenseKey}
function licenseRefFor(tenantId, appId, licenseKey) {
  return db.ref(`tenants/${tenantId}/licenses/${appId}/${licenseKey}`);
}

// ─────────────────────────────────────────────────────────────
//  issueToken  (unauthenticated)
// ─────────────────────────────────────────────────────────────
//
// Client POSTs { licenseKey, username, deviceId, appId, tenantId? } and
// receives { token, tier, expiresIn, uid, appId, tenantId, userId }.
//
// tenantId defaults to "ces" for legacy clients that predate the claim. The minted token
// carries: tier, app_id, tenant_id, user_id, license_key, device_id.
// NOTE: no `username` claim -- rules and downstream functions work off the
// hashed user_id only.
exports.issueToken = onCall(
  {
    region: REGION,
    cors: true,
    maxInstances: 20,
    invoker: "public",
    secrets: ALL_TENANT_SECRETS,
  },
  async (req) => {
    const data = req.data || {};
    const licenseKey = String(data.licenseKey || "").trim();
    const rawUsername = String(data.username || "").trim();
    const deviceId = String(data.deviceId || "").trim();
    const rawAppId = String(data.appId || "desktophub").trim().toLowerCase();
    const rawTenantId = String(data.tenantId || "ces").trim().toLowerCase();

    if (!licenseKey || !rawUsername || !deviceId) {
      throw new HttpsError("invalid-argument",
        "missing licenseKey, username, or deviceId");
    }
    if (!ALLOWED_APP_IDS.has(rawAppId)) {
      throw new HttpsError("invalid-argument",
        `unknown appId '${rawAppId}' (allowed: ${[...ALLOWED_APP_IDS].join(", ")})`);
    }
    if (!ALLOWED_TENANT_IDS.has(rawTenantId)) {
      throw new HttpsError("invalid-argument",
        `unknown tenantId '${rawTenantId}'`);
    }

    const appId = rawAppId;
    // tenantName = plaintext (admin-facing, used for Secret Manager lookup)
    // tenantKey  = hash    (DB-facing, used for every RTDB path + the claim)
    const tenantName = rawTenantId;
    const tenantKey = tenantKeyFor(tenantName);
    const username = rawUsername.toLowerCase();
    const userId = hmacUserId(tenantName, username);

    // License validation -- tenant-scoped, per-app path.
    const licenseRef = licenseRefFor(tenantKey, appId, licenseKey);
    const licSnap = await licenseRef.get();

    if (!licSnap.exists()) {
      logger.info("issueToken: license not found",
        { appId, tenantName, tenantKey, licenseKey });
      if (/^FREE-[A-Za-z0-9-]+$/.test(licenseKey)) {
        await licenseRef.set({
          license_key: licenseKey,
          app_id: appId,
          tenant_key: tenantKey,
          plan: "free",
          status: "active",
          created_at: new Date().toISOString(),
          source: "auto-provisioned",
        });
      } else {
        throw new HttpsError("permission-denied", "unknown license");
      }
    }

    // Role resolution under the tenant namespace. dev > admin > user.
    const [adminSnap, devSnap] = await Promise.all([
      db.ref(`tenants/${tenantKey}/admin_users/${userId}`).get(),
      db.ref(`tenants/${tenantKey}/dev_users/${userId}`).get(),
    ]);

    let tier = "user";
    if (devSnap.val() === true) tier = "dev";
    else if (adminSnap.val() === true) tier = "admin";

    // Refresh the encrypted username + auth timestamp on the user's profile
    // node. Stored under users/{user_id} alongside device linkages; the
    // admin-only listTenantUsers decrypts username_ct server-side. Using
    // .update() so we don't clobber the client-owned fields of this node.
    const userRef = db.ref(`tenants/${tenantKey}/users/${userId}`);
    await userRef.update({
      username_ct: encryptUsername(tenantName, username),
      last_seen: new Date().toISOString(),
    });

    const prefix = UID_PREFIX[appId] || "app_";
    // Firebase uid -- per-app + per-tenant + per-user, all hashed. No raw
    // username AND no plaintext tenant name leaks into the auth system.
    const uid = `${prefix}${tenantKey}_${userId}`;

    // Minted claim: tenant_id is the HASH. Clients never see the plaintext
    // tenant name again (they sent it in the request, we verified it, but
    // from here on everything -- paths, rules, audit logs -- uses the hash).
    const customToken = await auth.createCustomToken(uid, {
      tier,
      app_id: appId,
      tenant_id: tenantKey,
      user_id: userId,
      license_key: licenseKey,
      device_id: deviceId,
    });

    logger.info("issueToken: minted",
      { appId, tenantName, tenantKey, userId, tier, deviceId });

    return {
      token: customToken,
      tier,
      expiresIn: TOKEN_TTL_SECONDS,
      uid,
      appId,
      tenantId: tenantKey,   // client stores the hash as its tenant id
      userId,
    };
  }
);

// ─────────────────────────────────────────────────────────────
//  setRole  (dev-only; admin-only for 'admin' grants)
// ─────────────────────────────────────────────────────────────
//
// Grants or revokes a role for a raw username under a tenant. Admin callers
// can only grant/revoke 'admin'; dev callers can grant/revoke either.
//
// Input:  { username, tenantId, role: 'admin'|'dev'|'none' }
// Writes: /tenants/{tenantId}/{admin_users|dev_users}/{user_id}
exports.setRole = onCall(
  {
    region: REGION,
    cors: true,
    maxInstances: 5,
    invoker: "public",
    secrets: ALL_TENANT_SECRETS,
  },
  async (req) => {
    if (!req.auth) {
      throw new HttpsError("unauthenticated", "sign in required");
    }
    const callerTier = req.auth.token.tier;
    const d = req.data || {};
    const targetUsername = String(d.username || "").trim();
    const tenantId = String(d.tenantId || "ces").trim().toLowerCase();
    const role = String(d.role || "").trim().toLowerCase();

    if (!targetUsername) {
      throw new HttpsError("invalid-argument", "username required");
    }
    if (!ALLOWED_TENANT_IDS.has(tenantId)) {
      throw new HttpsError("invalid-argument", `unknown tenant '${tenantId}'`);
    }
    if (!["admin", "dev", "none"].includes(role)) {
      throw new HttpsError("invalid-argument", "role must be admin|dev|none");
    }

    // tenantId in the request body is the plaintext tenant NAME (for
    // Secret Manager lookup). Derive the hash that the DB + rules use.
    // Verify it matches the caller's claim (which may be hash OR plaintext
    // for back-compat with tokens minted before the hashing change).
    const tenantKey = tenantKeyFor(tenantId);
    const callerCtx = resolveCallerTenant(req.auth.token.tenant_id);
    if (tenantKey !== callerCtx.key) {
      throw new HttpsError("permission-denied", "cross-tenant writes forbidden");
    }

    // Permission: dev can set any role. admin can only set admin or revoke admin.
    if (callerTier !== "dev") {
      if (callerTier !== "admin") {
        throw new HttpsError("permission-denied", "admin or dev tier required");
      }
      if (role === "dev") {
        throw new HttpsError("permission-denied", "dev tier can only be granted by dev");
      }
    }

    const targetUserId = hmacUserId(tenantId, targetUsername);
    const adminRef = db.ref(`tenants/${tenantKey}/admin_users/${targetUserId}`);
    const devRef = db.ref(`tenants/${tenantKey}/dev_users/${targetUserId}`);
    const userRef = db.ref(`tenants/${tenantKey}/users/${targetUserId}`);

    await userRef.update({
      username_ct: encryptUsername(tenantId, targetUsername.toLowerCase()),
      updated_at: new Date().toISOString(),
    });

    if (role === "admin") {
      await adminRef.set(true);
    } else if (role === "dev") {
      await devRef.set(true);
    } else {
      if (callerTier === "dev") {
        await Promise.all([adminRef.remove(), devRef.remove()]);
      } else {
        await adminRef.remove();
      }
    }

    logger.info("setRole",
      { tenantKey, targetUserId, role, by: req.auth.token.user_id });

    return { ok: true, tenantId: tenantKey, userId: targetUserId, role };
  }
);

// ─────────────────────────────────────────────────────────────
//  checkRole  (admin/dev)
// ─────────────────────────────────────────────────────────────
//
// Given a raw username, returns the current role under the tenant. Lets
// trusted CLI / Dev Panel look up individuals without ever exposing the
// hash table. There is intentionally no listAll endpoint -- enumerating
// admins requires knowing the candidate usernames.
exports.checkRole = onCall(
  {
    region: REGION,
    cors: true,
    maxInstances: 5,
    invoker: "public",
    secrets: ALL_TENANT_SECRETS,
  },
  async (req) => {
    if (!req.auth || !["admin", "dev"].includes(req.auth.token.tier)) {
      throw new HttpsError("permission-denied", "admin or dev tier required");
    }
    const d = req.data || {};
    const targetUsername = String(d.username || "").trim();
    const tenantName = String(d.tenantId || "ces").trim().toLowerCase();

    if (!targetUsername) {
      throw new HttpsError("invalid-argument", "username required");
    }
    if (!ALLOWED_TENANT_IDS.has(tenantName)) {
      throw new HttpsError("invalid-argument", `unknown tenant '${tenantName}'`);
    }
    const tenantKey = tenantKeyFor(tenantName);
    const callerCtx = resolveCallerTenant(req.auth.token.tenant_id);
    if (tenantKey !== callerCtx.key) {
      throw new HttpsError("permission-denied", "cross-tenant reads forbidden");
    }

    const userId = hmacUserId(tenantName, targetUsername);
    const [adminSnap, devSnap] = await Promise.all([
      db.ref(`tenants/${tenantKey}/admin_users/${userId}`).get(),
      db.ref(`tenants/${tenantKey}/dev_users/${userId}`).get(),
    ]);

    let role = "user";
    if (devSnap.val() === true) role = "dev";
    else if (adminSnap.val() === true) role = "admin";

    return { role, tenantId: tenantKey, userId };
  }
);

// ─────────────────────────────────────────────────────────────
//  setCheatSheetEditor  (admin/dev)
// ─────────────────────────────────────────────────────────────
//
// Grants or revokes cheat-sheet editor permission for a raw username under
// the caller's tenant. Stored at /tenants/{tenantId}/cheat_sheet_editors/{user_id}.
exports.setCheatSheetEditor = onCall(
  {
    region: REGION,
    cors: true,
    maxInstances: 5,
    invoker: "public",
    secrets: ALL_TENANT_SECRETS,
  },
  async (req) => {
    if (!req.auth || !["admin", "dev"].includes(req.auth.token.tier)) {
      throw new HttpsError("permission-denied", "admin or dev tier required");
    }
    const d = req.data || {};
    const targetUsername = String(d.username || "").trim();
    // Plaintext tenant name from request body; hashed claim on the token.
    const callerCtx = resolveCallerTenant(req.auth.token.tenant_id);
    const tenantName = String(d.tenantId || "").trim().toLowerCase() || callerCtx.name;
    const tenantKey = tenantKeyFor(tenantName);
    const enabled = d.enabled === true || d.enabled === "true";

    if (!targetUsername) {
      throw new HttpsError("invalid-argument", "username required");
    }
    if (!ALLOWED_TENANT_IDS.has(tenantName)) {
      throw new HttpsError("invalid-argument", `unknown tenant '${tenantName}'`);
    }
    if (tenantKey !== callerCtx.key) {
      throw new HttpsError("permission-denied", "cross-tenant writes forbidden");
    }

    const targetUserId = hmacUserId(tenantName, targetUsername);
    const ref = db.ref(`tenants/${tenantKey}/cheat_sheet_editors/${targetUserId}`);
    const userRef = db.ref(`tenants/${tenantKey}/users/${targetUserId}`);

    await userRef.update({
      username_ct: encryptUsername(tenantName, targetUsername.toLowerCase()),
      updated_at: new Date().toISOString(),
    });

    if (enabled) {
      await ref.set(true);
    } else {
      await ref.remove();
    }

    logger.info("setCheatSheetEditor",
      { tenantKey, targetUserId, enabled, by: req.auth.token.user_id });
    return { ok: true, tenantId: tenantKey, userId: targetUserId, enabled };
  }
);

// ─────────────────────────────────────────────────────────────
//  listTenantUsers  (admin/dev, same tenant only)
// ─────────────────────────────────────────────────────────────
//
// Returns every directory entry for the caller's tenant with the username
// decrypted server-side, plus each user's admin/dev flags. Callers cannot
// list other tenants -- hard isolation by claim.
exports.listTenantUsers = onCall(
  {
    region: REGION,
    cors: true,
    maxInstances: 5,
    invoker: "public",
    secrets: ALL_TENANT_SECRETS,
  },
  async (req) => {
    if (!req.auth || !["admin", "dev"].includes(req.auth.token.tier)) {
      throw new HttpsError("permission-denied", "admin or dev tier required");
    }
    // The claim carries the HASH (or the plaintext for tokens minted before
    // the hashing change -- resolveCallerTenant handles both). Resolve to
    // both forms so we can use the hash for paths and the name for the AES
    // key lookup that decrypts username_ct.
    const { name: callerTenantName, key: callerTenantKey } =
      resolveCallerTenant(req.auth.token.tenant_id);

    // Read from BOTH the hashed path (current) and the legacy plaintext path
    // (pre-hashing) so admins still see users mid-migration without losing
    // names. Merge results: hashed-path entries win on collision (newest).
    const [usersHash, usersLegacy, adminHash, adminLegacy, devHash, devLegacy,
           editorHash, editorLegacy] = await Promise.all([
      db.ref(`tenants/${callerTenantKey}/users`).get(),
      db.ref(`tenants/${callerTenantName}/users`).get(),
      db.ref(`tenants/${callerTenantKey}/admin_users`).get(),
      db.ref(`tenants/${callerTenantName}/admin_users`).get(),
      db.ref(`tenants/${callerTenantKey}/dev_users`).get(),
      db.ref(`tenants/${callerTenantName}/dev_users`).get(),
      db.ref(`tenants/${callerTenantKey}/cheat_sheet_editors`).get(),
      db.ref(`tenants/${callerTenantName}/cheat_sheet_editors`).get(),
    ]);

    const profiles = { ...(usersLegacy.val() || {}), ...(usersHash.val() || {}) };
    const admins = { ...(adminLegacy.val() || {}), ...(adminHash.val() || {}) };
    const devs = { ...(devLegacy.val() || {}), ...(devHash.val() || {}) };
    const editors = { ...(editorLegacy.val() || {}), ...(editorHash.val() || {}) };

    const users = [];
    for (const [userId, entry] of Object.entries(profiles)) {
      let username = "";
      try {
        username = entry?.username_ct
          ? decryptUsername(callerTenantName, entry.username_ct)
          : "";
      } catch (e) {
        logger.warn("listTenantUsers: decrypt failed",
          { tenantKey: callerTenantKey, userId });
        username = "[decrypt-failed]";
      }
      users.push({
        userId,
        username,
        isAdmin: admins[userId] === true,
        isDev: devs[userId] === true,
        isEditor: editors[userId] === true,
        lastSeen: entry?.last_seen || null,
      });
    }

    users.sort((a, b) => a.username.localeCompare(b.username));
    return { tenantId: callerTenant, users };
  }
);

// ─────────────────────────────────────────────────────────────
//  pushForceUpdate  (admin/dev)
// ─────────────────────────────────────────────────────────────
exports.pushForceUpdate = onCall(
  { region: REGION, cors: true, maxInstances: 5, invoker: "public" },
  async (req) => {
    if (!req.auth || !["admin", "dev"].includes(req.auth.token.tier)) {
      throw new HttpsError("permission-denied", "admin or dev tier required");
    }

    const d = req.data || {};
    const deviceId = String(d.deviceId || "").trim();
    const targetVersion = String(d.targetVersion || "").trim();
    const downloadUrl = String(d.downloadUrl || "").trim();

    if (!deviceId || !targetVersion || !downloadUrl) {
      throw new HttpsError("invalid-argument",
        "deviceId, targetVersion, downloadUrl required");
    }

    const payload = {
      target_version: targetVersion,
      download_url: downloadUrl,
      pushed_by: req.auth.token.user_id || "unknown",
      pushed_at: new Date().toISOString(),
      status: "pending",
      retry_count: 0,
      app_id: d.appId || "desktophub",
    };

    await db.ref(`force_update/${deviceId}`).set(payload);
    logger.info("pushForceUpdate",
      { deviceId, targetVersion, pushedBy: payload.pushed_by });

    return { ok: true };
  }
);

// ─────────────────────────────────────────────────────────────
//  clearForceUpdate  (admin/dev)
// ─────────────────────────────────────────────────────────────
exports.clearForceUpdate = onCall(
  { region: REGION, cors: true, maxInstances: 5, invoker: "public" },
  async (req) => {
    if (!req.auth || !["admin", "dev"].includes(req.auth.token.tier)) {
      throw new HttpsError("permission-denied", "admin or dev tier required");
    }

    const d = req.data || {};
    const deviceId = String(d.deviceId || "").trim();
    if (!deviceId) throw new HttpsError("invalid-argument", "deviceId required");

    await db.ref(`force_update/${deviceId}`).remove();
    return { ok: true };
  }
);
