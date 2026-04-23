"""Subscription management for Spec Header Updater.

2026-04-23 rewrite: dropped pyrebase + firebase-admin + anonymous Firebase Auth.
All Firebase interaction now goes through the issueToken Cloud Function
(see firebase_auth.py), which mints a short-lived custom token carrying the
caller's license + tier + app_id claims. Every RTDB REST call is gated by
those claims via database.rules.json.

What that means in practice:
  * No service-account credentials ship in the client binary.
  * The `firebase-admin-key.json` that used to live in the repo is gone.
  * `pyrebase` and `firebase-admin` are no longer required at runtime.
  * The raw MAC address never leaves the machine (see metrics.get_mac_hash).

Public API preserved for the rest of the codebase:
  - is_subscribed() -> bool
  - ensure_license_exists() -> bool
  - get_subscription_info() -> dict
  - validate_license_key(key) -> bool
  - check_document_limit(requested_count=1) -> dict
  - record_document_processed(count=1) -> bool
  - refresh_subscription() -> bool
  - reset_subscription() -> None
"""
from __future__ import annotations

import hashlib
import json
import os
import secrets
import string
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any, Dict, Optional

try:
    from src import __version__ as APP_VERSION
except ImportError:
    try:
        from . import __version__ as APP_VERSION
    except ImportError:
        APP_VERSION = "1.0.0"

from src.build_config import TENANT_ID
from src.firebase_auth import FirebaseAuth, FirebaseAuthError
from src.metrics import get_device_fingerprint, get_user_identifier, get_mac_hash


# ─────────────────────────────────────────────────────────────────────────────
# Constants
# ─────────────────────────────────────────────────────────────────────────────
RTDB_BASE_URL = "https://licenses-ff136-default-rtdb.firebaseio.com"

_DEFAULT_HTTP_TIMEOUT = 15


# ─────────────────────────────────────────────────────────────────────────────
# Subscription manager
# ─────────────────────────────────────────────────────────────────────────────
class SubscriptionManager:
    """Manages subscription state for Spec Header Updater.

    Holds a FirebaseAuth session and makes all network calls through the
    Cloud-Function-minted ID token. Local state lives under app_data_dir
    in a single subscription JSON file.
    """

    def __init__(
        self,
        app_id: str = "spec-updater",
        app_data_dir: Optional[Path] = None,
        config_path: Optional[Path] = None,  # ignored; kept for backward-compat
    ):
        self.app_id = app_id
        self.app_data_dir = app_data_dir or self._default_app_data_dir()
        self.app_data_dir.mkdir(parents=True, exist_ok=True)

        self.subscription_file = self.app_data_dir / f"subscription_{app_id}.json"
        self.device_id = self._get_or_create_device_id()
        self.device_fingerprint = get_device_fingerprint()
        self.username = os.environ.get(
            "USERNAME",
            os.environ.get("USER", os.getlogin() if hasattr(os, "getlogin") else "unknown"),
        ).lower()

        self._subscription_data: Optional[Dict[str, Any]] = None
        self._auth = FirebaseAuth(app_id=self.app_id)
        self._tenant_id = TENANT_ID

        # Best-effort: try to sign in up-front so later calls have a cached
        # token. If this fails (offline, Cloud Function down, etc.) we degrade
        # gracefully and the subscription checks return cached local state.
        self._ensure_signed_in()

    def _t(self, rel: str) -> str:
        """Prefix a relative path with this build's tenant namespace.

        Every user-scoped / license / device / telemetry path MUST go through
        here. Flat root-level writes are denied by the RTDB rules.
        """
        tid = self._auth.tenant_id or self._tenant_id
        return f"tenants/{tid}/{rel}"

    @property
    def _license_path(self) -> str:
        return self._t(f"licenses/{self.app_id}/")

    # ---- path helpers ----------------------------------------------------

    @staticmethod
    def _default_app_data_dir() -> Path:
        if os.name == "nt":
            base = Path(os.environ.get("LOCALAPPDATA", "~")).expanduser()
            return base / "SpecHeaderUpdater"
        return Path("~").expanduser() / ".config" / "specheadupdater"

    def _get_or_create_device_id(self) -> str:
        device_id_file = self.app_data_dir / "device_id.txt"
        if device_id_file.exists():
            existing = device_id_file.read_text().strip()
            if existing:
                return existing
        # Derive deterministically from local info so reinstalls reuse the
        # same id. The INPUT to this hash includes the raw MAC, but the
        # OUTPUT is a 64-hex-char digest that reveals nothing about the MAC.
        import platform
        from src.metrics import MAC_ADDRESS as _RAW_MAC
        material = f"{platform.node()}|{_RAW_MAC}|{os.environ.get('USERNAME','')}"
        digest = hashlib.sha256(material.encode("utf-8")).hexdigest()
        new_id = digest[:32]
        device_id_file.write_text(new_id)
        return new_id

    # ---- auth / network helpers ------------------------------------------

    def _ensure_signed_in(self) -> bool:
        """Sign in once we know a license key.

        For apps that don't have a saved license yet, first launch may not
        be able to sign in until ensure_license_exists() provisions a FREE
        key -- the Cloud Function handles that auto-provisioning server-side
        when it sees a FREE-* key for which no record exists.
        """
        license_key = self._load_local_license_key() or self._generate_free_license_key()
        try:
            return self._auth.sign_in(license_key, self.username, self.device_id, self._tenant_id)
        except FirebaseAuthError as exc:
            print(f"SubscriptionManager: sign-in failed -- {exc}")
            return False

    def _rtdb_get(self, path: str) -> Optional[Any]:
        token = self._auth.get_id_token()
        if not token:
            return None
        url = f"{RTDB_BASE_URL}/{path}.json?auth={urllib.parse.quote(token, safe='')}"
        try:
            with urllib.request.urlopen(url, timeout=_DEFAULT_HTTP_TIMEOUT) as resp:
                raw = resp.read().decode("utf-8")
                if not raw or raw == "null":
                    return None
                return json.loads(raw)
        except Exception as exc:
            print(f"SubscriptionManager._rtdb_get({path}): {exc}")
            return None

    def _rtdb_patch(self, path: str, data: Dict[str, Any]) -> bool:
        token = self._auth.get_id_token()
        if not token:
            return False
        url = f"{RTDB_BASE_URL}/{path}.json?auth={urllib.parse.quote(token, safe='')}"
        req = urllib.request.Request(
            url, data=json.dumps(data).encode("utf-8"),
            method="PATCH", headers={"Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(req, timeout=_DEFAULT_HTTP_TIMEOUT) as resp:
                return 200 <= resp.status < 300
        except Exception as exc:
            print(f"SubscriptionManager._rtdb_patch({path}): {exc}")
            return False

    def _rtdb_put(self, path: str, data: Any) -> bool:
        token = self._auth.get_id_token()
        if not token:
            return False
        url = f"{RTDB_BASE_URL}/{path}.json?auth={urllib.parse.quote(token, safe='')}"
        req = urllib.request.Request(
            url, data=json.dumps(data).encode("utf-8"),
            method="PUT", headers={"Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(req, timeout=_DEFAULT_HTTP_TIMEOUT) as resp:
                return 200 <= resp.status < 300
        except Exception as exc:
            print(f"SubscriptionManager._rtdb_put({path}): {exc}")
            return False

    # ---- local subscription state ----------------------------------------

    def _load_local_license_key(self) -> Optional[str]:
        sub = self._load_subscription()
        return sub.get("license_key") if sub else None

    def _load_subscription(self) -> Optional[Dict[str, Any]]:
        if self._subscription_data is not None:
            return self._subscription_data
        if not self.subscription_file.exists():
            return None
        try:
            self._subscription_data = json.loads(self.subscription_file.read_text())
            return self._subscription_data
        except (json.JSONDecodeError, OSError):
            return None

    def _save_subscription(self, data: Dict[str, Any]) -> None:
        self._subscription_data = data
        try:
            self.subscription_file.write_text(json.dumps(data, indent=2))
        except OSError as exc:
            print(f"SubscriptionManager: failed to persist subscription: {exc}")

    @staticmethod
    def _generate_free_license_key() -> str:
        alpha = string.ascii_uppercase + string.digits
        hash_part = hashlib.md5(os.urandom(16)).hexdigest()[:8].upper()
        rand_part = "".join(secrets.choice(alpha) for _ in range(8))
        return f"FREE-{hash_part}-{rand_part}"

    # ---- public API ------------------------------------------------------

    def ensure_license_exists(self) -> bool:
        """Ensure this device has a license record (creates a FREE one if needed).

        The Cloud Function auto-provisions a FREE-* license on first sign_in,
        so most paths here just confirm and persist locally.
        """
        local = self._load_subscription()
        if local and local.get("license_key"):
            return True

        # First run: generate + save a FREE key, sign in again so the Cloud
        # Function provisions it server-side.
        license_key = self._generate_free_license_key()
        self._save_subscription({
            "license_key": license_key,
            "app_id": self.app_id,
            "plan": "free",
            "status": "active",
            "created_at": datetime.now(timezone.utc).isoformat(),
        })
        ok = self._auth.sign_in(license_key, self.username, self.device_id, self._tenant_id)
        if not ok:
            return False

        # Register the device record (best-effort -- subscription still works
        # locally even if the registration call is denied by rules).
        self._register_device(license_key)
        return True

    def _register_device(self, license_key: str) -> None:
        """Record device metadata server-side. PII-safe: MAC hashed, username
        replaced with the HMAC'd user_id claim from the token. Raw Windows
        username never hits the database."""
        now = datetime.now(timezone.utc).isoformat()
        device_data = {
            "device_name": os.environ.get("COMPUTERNAME", "Unknown"),
            "user_id": self._auth.user_id or "",
            "mac_hash": get_mac_hash(),
            "platform": self.device_fingerprint.get("platform", ""),
            "platform_version": self.device_fingerprint.get("platform_version", ""),
            "machine": self.device_fingerprint.get("machine", ""),
            "last_seen": now,
            "status": "active",
            "license_key": license_key,
            "app_id": self.app_id,
        }
        # Shared tenant-scoped devices namespace; multi-app isolation comes
        # from the app_id field + rule checks on the token's app_id claim.
        self._rtdb_patch(self._t(f"devices/{self.device_id}"), device_data)

    def is_subscribed(self) -> bool:
        sub = self._load_subscription()
        if not sub:
            return self.ensure_license_exists() and bool(self._load_subscription())
        if not sub.get("license_key"):
            return False

        expiry = sub.get("expiry_date")
        if expiry:
            try:
                return datetime.now(timezone.utc) < datetime.fromisoformat(expiry)
            except ValueError:
                return True
        # Free licenses never expire.
        return True

    def get_subscription_info(self) -> Dict[str, Any]:
        sub = self._load_subscription() or {}
        return {
            "status": sub.get("status", "unknown"),
            "plan": sub.get("plan", "free"),
            "license_key": sub.get("license_key"),
            "tier": self._auth.tier or "user",
            "ready": self._auth.is_ready,
            "message": sub.get("message") or "OK",
        }

    def validate_license_key(self, license_key: str) -> bool:
        """Validate a user-provided license key against Firebase."""
        if not license_key or not license_key.strip():
            return False
        license_key = license_key.strip()
        # Swap in the new key and attempt a fresh sign-in with it. If the
        # server rejects (unknown license, etc.) the sign-in fails and we
        # restore the prior state.
        prior = self._load_subscription()
        ok = self._auth.sign_in(license_key, self.username, self.device_id, self._tenant_id)
        if not ok:
            return False

        lic = self._rtdb_get(self._t(f"licenses/{self.app_id}/{license_key}"))
        if not isinstance(lic, dict) or not lic.get("license_key"):
            # Revert
            if prior and prior.get("license_key"):
                self._auth.sign_in(prior["license_key"], self.username, self.device_id, self._tenant_id)
            return False

        self._save_subscription({
            "license_key": license_key,
            "app_id": self.app_id,
            "plan": lic.get("plan", "free"),
            "status": lic.get("status", "active"),
            "expiry_date": lic.get("expiry_date"),
            "created_at": lic.get("created_at") or datetime.now(timezone.utc).isoformat(),
        })
        self._register_device(license_key)
        return True

    def check_document_limit(self, requested_count: int = 1) -> Dict[str, Any]:
        """Does this user have the budget to process `requested_count` more docs?

        Free tier has a documented cap; paid tiers are unlimited. We keep the
        counter in local state only -- the server doesn't enforce the cap in
        the rewrite (simpler; the tier claim already gates the feature set).
        """
        sub = self._load_subscription() or {}
        plan = sub.get("plan", "free")
        if plan != "free":
            return {"allowed": True, "remaining": None, "plan": plan}

        monthly_cap = int(sub.get("monthly_cap", 100))
        used_this_month = int(sub.get("used_this_month", 0))
        remaining = max(0, monthly_cap - used_this_month)
        return {
            "allowed": remaining >= requested_count,
            "remaining": remaining,
            "plan": plan,
            "monthly_cap": monthly_cap,
            "used_this_month": used_this_month,
        }

    def record_document_processed(self, count: int = 1) -> bool:
        sub = self._load_subscription() or {}
        sub["used_this_month"] = int(sub.get("used_this_month", 0)) + int(count)
        sub["last_processed_at"] = datetime.now(timezone.utc).isoformat()
        self._save_subscription(sub)

        # Fire a best-effort usage event; failure is not fatal.
        license_key = sub.get("license_key")
        if license_key:
            self._rtdb_patch(
                self._t(f"licenses/{self.app_id}/{license_key}/usage/{self.device_id}"),
                {
                    "last_processed_at": sub["last_processed_at"],
                    "used_this_month": sub["used_this_month"],
                },
            )
        return True

    # ---- backward-compat shims -------------------------------------------
    # These exist because main.py calls into them. Keep them no-op-safe.

    def _sync_activation_status(self) -> bool:
        # No separate activation flow in the new model -- having a valid
        # ID token IS the activation. Just confirm we can still sign in.
        return self._auth.is_ready or self._ensure_signed_in()

    def _update_license_usage(self, usage_data: Dict[str, Any]) -> bool:
        sub = self._load_subscription() or {}
        sub.update(usage_data or {})
        self._save_subscription(sub)
        return True

    def refresh_subscription(self) -> bool:
        sub = self._load_subscription()
        if not sub or not sub.get("license_key"):
            return self.ensure_license_exists()
        lic = self._rtdb_get(self._t(f"licenses/{self.app_id}/{sub['license_key']}"))
        if isinstance(lic, dict):
            sub.update({
                "plan": lic.get("plan", sub.get("plan")),
                "status": lic.get("status", sub.get("status")),
                "expiry_date": lic.get("expiry_date"),
            })
            self._save_subscription(sub)
            return True
        return False

    def reset_subscription(self) -> None:
        """Wipe local subscription state. Next run will auto-provision fresh."""
        try:
            self.subscription_file.unlink(missing_ok=True)
        except TypeError:
            # Python <3.8 doesn't support missing_ok
            if self.subscription_file.exists():
                self.subscription_file.unlink()
        self._subscription_data = None
