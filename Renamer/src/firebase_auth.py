"""Client-side Firebase Auth lifecycle for Spec Header Updater.

Mirrors the pattern used by DesktopHub's FirebaseAuth.cs:

  1. POST to the `issueToken` Cloud Function with
     {appId, licenseKey, username, deviceId}. The function validates the
     license, resolves the caller's tier (user / dev / admin) from
     Firebase role nodes, and returns a short-lived (1-hour) Firebase
     custom token whose claims carry the tier + app identity.
  2. Exchange the custom token for a Firebase ID token via the Identity
     Toolkit `signInWithCustomToken` REST endpoint.
  3. Cache the ID token + refresh token. Refresh transparently a few
     minutes before expiry.

All subsequent Realtime Database REST calls attach the ID token as
`?auth=<idToken>`. The server enforces tier- and app-scoped rules
against the token's claims -- the client therefore holds *zero*
service-account credentials and cannot do anything a non-admin user
isn't already authorized to do.

The Web API key hardcoded below is public information (it identifies
the Firebase project for public APIs like Identity Toolkit) and is
NOT a credential. See README / docs for background.
"""
from __future__ import annotations

import json
import threading
import time
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from typing import Any, Dict, Optional


# Web API key for the licenses-ff136 Firebase project. Public by design --
# shipped in every Firebase web/mobile app. Grants no privileged access on
# its own; writes are still gated by custom-token claims + database rules.
WEB_API_KEY = "AIzaSyBTmqZ6HMdKEoR8bMtSka-gUm_XiqwLYHM"

ISSUE_TOKEN_URL = (
    "https://us-central1-licenses-ff136.cloudfunctions.net/issueToken"
)
SIGN_IN_WITH_CUSTOM_TOKEN_URL = (
    "https://identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken"
    f"?key={WEB_API_KEY}"
)
REFRESH_TOKEN_URL = (
    f"https://securetoken.googleapis.com/v1/token?key={WEB_API_KEY}"
)

# Refresh this many seconds BEFORE the token's stated expiry to avoid racing
# with a server-side clock skew or network latency.
REFRESH_GRACE_SECONDS = 5 * 60

_HTTP_TIMEOUT_SECONDS = 15


class FirebaseAuthError(RuntimeError):
    """Raised when the client cannot obtain or refresh a Firebase ID token."""


class FirebaseAuth:
    """Owns the sign-in -> refresh cycle for a single app session.

    Thread-safe via a single lock; call GetIdToken from any thread.
    """

    def __init__(self, app_id: str):
        self._app_id = app_id
        self._lock = threading.Lock()

        self._id_token: Optional[str] = None
        self._refresh_token: Optional[str] = None
        self._expires_at_utc: datetime = datetime.fromtimestamp(0, tz=timezone.utc)
        self._tier: Optional[str] = None
        self._user_id: Optional[str] = None   # HMAC hash from issueToken claim
        self._tenant_id: Optional[str] = None

        # Remembered args so we can re-sign-in if the refresh path fails.
        self._license_key: Optional[str] = None
        self._username: Optional[str] = None
        self._device_id: Optional[str] = None

    # ----- public API ------------------------------------------------------

    @property
    def tier(self) -> Optional[str]:
        return self._tier

    @property
    def user_id(self) -> Optional[str]:
        """Hashed user id issued by the Cloud Function. Use for every
        user-keyed RTDB path. Raw Windows username never leaves memory."""
        return self._user_id

    @property
    def tenant_id(self) -> Optional[str]:
        return self._tenant_id

    @property
    def is_ready(self) -> bool:
        return bool(self._id_token) and datetime.now(timezone.utc) < self._expires_at_utc

    def sign_in(self, license_key: str, username: str, device_id: str,
                tenant_id: str) -> bool:
        """Initial handshake. Idempotent -- re-calling before expiry is a no-op."""
        with self._lock:
            self._license_key = license_key
            self._username = username.lower()
            self._device_id = device_id
            self._tenant_id = tenant_id

            if self.is_ready:
                return True
            return self._do_sign_in_locked()

    def get_id_token(self) -> Optional[str]:
        """Return a valid ID token, refreshing transparently if needed.

        Returns None when we don't have prior context (sign_in was never
        called, or failed, and we have no refresh token cached).
        """
        with self._lock:
            now = datetime.now(timezone.utc)
            if self._id_token and (now + timedelta(seconds=REFRESH_GRACE_SECONDS)) < self._expires_at_utc:
                return self._id_token

            # Prefer a cheap refresh when we have a refresh_token; fall back
            # to a fresh sign-in otherwise.
            if self._refresh_token and self._try_refresh_locked():
                return self._id_token

            if self._license_key and self._username and self._device_id:
                if self._do_sign_in_locked():
                    return self._id_token

            return None

    def call_function(self, function_name: str, payload: Dict[str, Any]) -> Optional[Dict[str, Any]]:
        """Invoke a Firebase callable-formatted function with the current ID token.

        Callers that need to reach admin-tier Cloud Functions
        (e.g. pushForceUpdate) use this; the request is authenticated by the
        caller's tier claim, the function then checks req.auth.token.tier
        server-side before executing any privileged operation.
        """
        id_token = self.get_id_token()
        if not id_token:
            return None

        url = f"https://us-central1-licenses-ff136.cloudfunctions.net/{function_name}"
        body = {"data": payload}
        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {id_token}",
        }
        try:
            resp_json = _http_post_json(url, body, headers=headers)
        except Exception as exc:
            print(f"FirebaseAuth.call_function({function_name}) failed: {exc}")
            return None

        return resp_json.get("result") if isinstance(resp_json, dict) else None

    # ----- locked internals ------------------------------------------------

    def _do_sign_in_locked(self) -> bool:
        """Full round-trip: issueToken -> signInWithCustomToken.

        Caller already holds self._lock.
        """
        try:
            issue_body = {
                "data": {
                    "appId": self._app_id,
                    "licenseKey": self._license_key,
                    "username": self._username,
                    "deviceId": self._device_id,
                    "tenantId": self._tenant_id,
                }
            }
            issue_resp = _http_post_json(
                ISSUE_TOKEN_URL, issue_body,
                headers={"Content-Type": "application/json"},
            )
            result = issue_resp.get("result") if isinstance(issue_resp, dict) else None
            if not result:
                print(f"FirebaseAuth.sign_in: issueToken response missing 'result': {issue_resp!r}")
                return False

            custom_token = result.get("token")
            self._tier = result.get("tier", "user")
            self._user_id = result.get("userId")
            if result.get("tenantId"):
                self._tenant_id = result.get("tenantId")
            if not custom_token:
                print("FirebaseAuth.sign_in: empty custom token from issueToken")
                return False

            # Exchange the custom token for a Firebase ID token.
            exch_resp = _http_post_json(
                SIGN_IN_WITH_CUSTOM_TOKEN_URL,
                {"token": custom_token, "returnSecureToken": True},
                headers={"Content-Type": "application/json"},
            )
            self._id_token = exch_resp.get("idToken")
            self._refresh_token = exch_resp.get("refreshToken")
            expires_in = int(exch_resp.get("expiresIn", 3600))
            self._expires_at_utc = datetime.now(timezone.utc) + timedelta(seconds=expires_in)

            # Never log raw username -- user_id is the non-reversible identifier.
            print(
                f"FirebaseAuth: signed in tenant={self._tenant_id} "
                f"user_id={self._user_id} tier={self._tier} "
                f"(expires in {expires_in}s)"
            )
            return bool(self._id_token)
        except Exception as exc:
            print(f"FirebaseAuth.sign_in: {type(exc).__name__}: {exc}")
            return False

    def _try_refresh_locked(self) -> bool:
        """Swap the refresh_token for a new id_token (cheaper than a full sign-in)."""
        try:
            form = urllib.parse.urlencode({
                "grant_type": "refresh_token",
                "refresh_token": self._refresh_token,
            }).encode()
            req = urllib.request.Request(
                REFRESH_TOKEN_URL, data=form, method="POST",
                headers={"Content-Type": "application/x-www-form-urlencoded"},
            )
            with urllib.request.urlopen(req, timeout=_HTTP_TIMEOUT_SECONDS) as r:
                body = json.loads(r.read().decode("utf-8") or "{}")

            self._id_token = body.get("id_token")
            self._refresh_token = body.get("refresh_token", self._refresh_token)
            expires_in = int(body.get("expires_in", 3600))
            self._expires_at_utc = datetime.now(timezone.utc) + timedelta(seconds=expires_in)
            return bool(self._id_token)
        except Exception as exc:
            print(f"FirebaseAuth.refresh: {type(exc).__name__}: {exc}")
            return False


# ----- helpers -----------------------------------------------------------


def _http_post_json(url: str, body: Dict[str, Any], *,
                     headers: Optional[Dict[str, str]] = None) -> Dict[str, Any]:
    data = json.dumps(body).encode("utf-8")
    req = urllib.request.Request(
        url, data=data, method="POST",
        headers=headers or {"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=_HTTP_TIMEOUT_SECONDS) as r:
        raw = r.read().decode("utf-8")
        return json.loads(raw) if raw else {}
