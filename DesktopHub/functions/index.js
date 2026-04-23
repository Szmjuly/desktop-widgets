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

function hmacUserId(tenantId, username) {
  const salt = getTenantSalt(tenantId);
  const normalized = String(username || "").trim().toLowerCase();
  if (!normalized) {
    throw new HttpsError("invalid-argument", "username required");
  }
  const mac = crypto.createHmac("sha256", salt).update(normalized).digest("hex");
  return mac.slice(0, USER_ID_HEX_LEN);
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
    const tenantId = rawTenantId;
    const username = rawUsername.toLowerCase();
    const userId = hmacUserId(tenantId, username);

    // License validation -- tenant-scoped, per-app path.
    const licenseRef = licenseRefFor(tenantId, appId, licenseKey);
    const licSnap = await licenseRef.get();

    if (!licSnap.exists()) {
      logger.info("issueToken: license not found",
        { appId, tenantId, licenseKey });
      if (/^FREE-[A-Za-z0-9-]+$/.test(licenseKey)) {
        await licenseRef.set({
          license_key: licenseKey,
          app_id: appId,
          tenant_id: tenantId,
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
      db.ref(`tenants/${tenantId}/admin_users/${userId}`).get(),
      db.ref(`tenants/${tenantId}/dev_users/${userId}`).get(),
    ]);

    let tier = "user";
    if (devSnap.val() === true) tier = "dev";
    else if (adminSnap.val() === true) tier = "admin";

    // Refresh the encrypted username + auth timestamp on the user's profile
    // node. Stored under users/{user_id} alongside device linkages etc; the
    // admin-only listTenantUsers decrypts username_ct server-side. Using
    // .update() so we don't clobber the client-owned fields of this node.
    const userRef = db.ref(`tenants/${tenantId}/users/${userId}`);
    await userRef.update({
      username_ct: encryptUsername(tenantId, username),
      last_seen: new Date().toISOString(),
    });

    const prefix = UID_PREFIX[appId] || "app_";
    // Firebase uid -- per-app, per-tenant, hashed. No raw username anywhere.
    const uid = `${prefix}${tenantId}_${userId}`;

    const customToken = await auth.createCustomToken(uid, {
      tier,
      app_id: appId,
      tenant_id: tenantId,
      user_id: userId,
      license_key: licenseKey,
      device_id: deviceId,
    });

    logger.info("issueToken: minted",
      { appId, tenantId, userId, tier, deviceId });

    return {
      token: customToken,
      tier,
      expiresIn: TOKEN_TTL_SECONDS,
      uid,
      appId,
      tenantId,
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
      throw new HttpsError("invalid-argument", `unknown tenantId '${tenantId}'`);
    }
    if (!["admin", "dev", "none"].includes(role)) {
      throw new HttpsError("invalid-argument", "role must be admin|dev|none");
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
    const adminRef = db.ref(`tenants/${tenantId}/admin_users/${targetUserId}`);
    const devRef = db.ref(`tenants/${tenantId}/dev_users/${targetUserId}`);
    const userRef = db.ref(`tenants/${tenantId}/users/${targetUserId}`);

    // Refresh the user's profile node with an encrypted username so admin
    // listings can resolve the grantee even if they've never signed in yet.
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
      { tenantId, targetUserId, role, by: req.auth.token.user_id });

    return { ok: true, tenantId, userId: targetUserId, role };
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
    const tenantId = String(d.tenantId || "ces").trim().toLowerCase();

    if (!targetUsername) {
      throw new HttpsError("invalid-argument", "username required");
    }
    if (!ALLOWED_TENANT_IDS.has(tenantId)) {
      throw new HttpsError("invalid-argument", `unknown tenantId '${tenantId}'`);
    }

    const userId = hmacUserId(tenantId, targetUsername);
    const [adminSnap, devSnap] = await Promise.all([
      db.ref(`tenants/${tenantId}/admin_users/${userId}`).get(),
      db.ref(`tenants/${tenantId}/dev_users/${userId}`).get(),
    ]);

    let role = "user";
    if (devSnap.val() === true) role = "dev";
    else if (adminSnap.val() === true) role = "admin";

    return { role, tenantId, userId };
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
    const tenantId = String(d.tenantId || req.auth.token.tenant_id || "ces")
      .trim().toLowerCase();
    const enabled = d.enabled === true || d.enabled === "true";

    if (!targetUsername) {
      throw new HttpsError("invalid-argument", "username required");
    }
    if (tenantId !== req.auth.token.tenant_id) {
      throw new HttpsError("permission-denied", "cross-tenant writes forbidden");
    }
    if (!ALLOWED_TENANT_IDS.has(tenantId)) {
      throw new HttpsError("invalid-argument", `unknown tenantId '${tenantId}'`);
    }

    const targetUserId = hmacUserId(tenantId, targetUsername);
    const ref = db.ref(`tenants/${tenantId}/cheat_sheet_editors/${targetUserId}`);
    const userRef = db.ref(`tenants/${tenantId}/users/${targetUserId}`);

    await userRef.update({
      username_ct: encryptUsername(tenantId, targetUsername.toLowerCase()),
      updated_at: new Date().toISOString(),
    });

    if (enabled) {
      await ref.set(true);
    } else {
      await ref.remove();
    }

    logger.info("setCheatSheetEditor",
      { tenantId, targetUserId, enabled, by: req.auth.token.user_id });
    return { ok: true, tenantId, userId: targetUserId, enabled };
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
    const callerTenant = req.auth.token.tenant_id;
    if (!callerTenant || !ALLOWED_TENANT_IDS.has(callerTenant)) {
      throw new HttpsError("permission-denied", "caller tenant unknown");
    }

    const [usersSnap, adminSnap, devSnap, editorSnap] = await Promise.all([
      db.ref(`tenants/${callerTenant}/users`).get(),
      db.ref(`tenants/${callerTenant}/admin_users`).get(),
      db.ref(`tenants/${callerTenant}/dev_users`).get(),
      db.ref(`tenants/${callerTenant}/cheat_sheet_editors`).get(),
    ]);

    const profiles = usersSnap.val() || {};
    const admins = adminSnap.val() || {};
    const devs = devSnap.val() || {};
    const editors = editorSnap.val() || {};

    const users = [];
    for (const [userId, entry] of Object.entries(profiles)) {
      let username = "";
      try {
        username = entry?.username_ct
          ? decryptUsername(callerTenant, entry.username_ct)
          : "";
      } catch (e) {
        logger.warn("listTenantUsers: decrypt failed",
          { tenantId: callerTenant, userId });
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
