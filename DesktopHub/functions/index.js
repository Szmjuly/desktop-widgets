/**
 * DesktopHub Cloud Functions
 *
 * These run server-side with Firebase Admin SDK privileges. The client binary
 * no longer embeds the admin service-account key; instead, clients call
 * `issueToken` to receive a short-lived Firebase Auth custom token scoped to
 * their tier (user / dev / admin). The client exchanges that for an ID token
 * and uses it on every RTDB call — database rules then enforce per-tier access.
 *
 * Privileged operations (pushing updates, editing feature flags, editing
 * licenses) go through callable functions like `pushForceUpdate` that check
 * `request.auth.token.tier === "admin"` server-side before acting.
 */

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const { logger } = require("firebase-functions/v2");
const admin = require("firebase-admin");

admin.initializeApp();
const db = admin.database();
const auth = admin.auth();

const REGION = "us-central1";
const TOKEN_TTL_SECONDS = 3600;

// ─────────────────────────────────────────────────────────────
//  issueToken  (unauthenticated)
// ─────────────────────────────────────────────────────────────
//
//  Client POSTs { licenseKey, username, deviceId } and receives
//  { token, tier, expiresIn }.
//
//  The client then exchanges `token` for a Firebase ID token via
//    POST https://identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken?key=<WEB_API_KEY>
//    body: { token, returnSecureToken: true }
//  and uses the resulting idToken as ?auth=<idToken> on all RTDB REST calls.
exports.issueToken = onCall(
  { region: REGION, cors: true, maxInstances: 20, invoker: "public" },
  async (req) => {
    const data = req.data || {};
    const licenseKey = String(data.licenseKey || "").trim();
    const rawUsername = String(data.username || "").trim();
    const deviceId = String(data.deviceId || "").trim();

    if (!licenseKey || !rawUsername || !deviceId) {
      throw new HttpsError("invalid-argument",
        "missing licenseKey, username, or deviceId");
    }

    const username = rawUsername.toLowerCase();

    // Validate license exists. We intentionally don't check license status/plan
    // here — existing clients treat FREE-AUTO licenses as valid; license state
    // enforcement can happen via database rules or a later tightening.
    const licSnap = await db.ref(`licenses/${licenseKey}`).get();
    if (!licSnap.exists()) {
      logger.info("issueToken: license not found", { licenseKey, username });
      // Auto-provision a FREE license the same way the old client did
      // so the first-run flow keeps working.
      if (/^FREE-[A-Za-z0-9-]+$/.test(licenseKey)) {
        await db.ref(`licenses/${licenseKey}`).set({
          license_key: licenseKey,
          app_id: "desktophub",
          plan: "free",
          status: "active",
          created_at: new Date().toISOString(),
          source: "auto-provisioned"
        });
      } else {
        throw new HttpsError("permission-denied", "unknown license");
      }
    }

    // Resolve tier: dev > admin > user.
    // "dev" is the superset -- it includes every admin privilege plus raw
    // database ops used by the developer panel. If a user is in both
    // dev_users and admin_users, we return "dev" because it grants strictly
    // more permissions.
    const [adminSnap, devSnap] = await Promise.all([
      db.ref(`admin_users/${username}`).get(),
      db.ref(`dev_users/${username}`).get()
    ]);

    let tier = "user";
    if (devSnap.val() === true) tier = "dev";
    else if (adminSnap.val() === true) tier = "admin";

    // The uid must be stable per-user so any Firebase Auth records, presence,
    // etc. line up across sessions. "dh_" prefix namespaces it.
    const uid = `dh_${username}`;

    const customToken = await auth.createCustomToken(uid, {
      tier,
      username,
      license_key: licenseKey,
      device_id: deviceId
    });

    logger.info("issueToken: minted", { username, tier, deviceId });

    return {
      token: customToken,
      tier,
      expiresIn: TOKEN_TTL_SECONDS,
      uid
    };
  }
);

// ─────────────────────────────────────────────────────────────
//  pushForceUpdate  (admin-only)
// ─────────────────────────────────────────────────────────────
//
//  Writes force_update/{deviceId}. Replaces the client's direct-write path
//  so no client ever needs write access to that node via the admin SDK.
exports.pushForceUpdate = onCall(
  { region: REGION, cors: true, maxInstances: 5, invoker: "public" },
  async (req) => {
    if (!req.auth || !["admin", "dev"].includes(req.auth.token.tier)) {
      throw new HttpsError("permission-denied",
        "admin or dev tier required");
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
      pushed_by: req.auth.token.username || "unknown",
      pushed_at: new Date().toISOString(),
      status: "pending",
      retry_count: 0,
      app_id: d.appId || "desktophub"
    };

    await db.ref(`force_update/${deviceId}`).set(payload);
    logger.info("pushForceUpdate", { deviceId, targetVersion,
      pushedBy: payload.pushed_by });

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
