"""Build-time and runtime configuration flags for Spec Header Updater.

Two levels of control:

1. BUILD-TIME, via `build_config.json` at the repo root.
   - `network_features_enabled` (bool, default True): master switch. When False,
     no Firebase, no license check, no telemetry, no auto-update. Also tells
     `build_exe.py` to exclude firebase_admin / pyrebase / google.auth from the
     bundled executable entirely, so the shipped .exe literally cannot reach
     outside the network.
   - `include_licensing` (bool, default True): legacy flag that only gates the
     SubscriptionManager. Superseded by `network_features_enabled`.

2. RUNTIME, via the `SPEC_UPDATER_OFFLINE` environment variable.
   - Set to any truthy value ("1", "true", "yes", "on") to force offline mode
     at runtime regardless of build flags. Lets IT test a networked build in
     air-gapped mode without a rebuild.

Consumers import the module-level constants:

    from src.build_config import NETWORK_FEATURES_ENABLED, INCLUDE_LICENSING

Both are resolved once at import time; there is no live reload.
"""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path

_CONFIG_FILE = Path(__file__).parent.parent / "build_config.json"


def _load_config() -> dict:
    if not _CONFIG_FILE.exists():
        return {}
    try:
        with open(_CONFIG_FILE, "r") as f:
            return json.load(f)
    except (json.JSONDecodeError, IOError):
        return {}


def _runtime_override_offline() -> bool:
    """Read SPEC_UPDATER_OFFLINE. Accepts 1/true/yes/on (case-insensitive)."""
    raw = os.environ.get("SPEC_UPDATER_OFFLINE", "").strip().lower()
    return raw in {"1", "true", "yes", "on"}


def _resolve_network_features_enabled(config: dict) -> bool:
    """Master switch: anything network-bound must consult this.

    Precedence:
      1. Runtime SPEC_UPDATER_OFFLINE=1  -> False (offline)  [highest]
      2. build_config.json network_features_enabled -> as-given
      3. No config file present -> True (networked, backward-compat)
    """
    if _runtime_override_offline():
        return False
    # Default True for backward compat when the field is absent
    return bool(config.get("network_features_enabled", True))


def _resolve_include_licensing(config: dict, network_enabled: bool) -> bool:
    """Legacy per-feature flag, forced off when the master switch is off."""
    if not network_enabled:
        return False
    return bool(config.get("include_licensing", True))


_CONFIG = _load_config()
NETWORK_FEATURES_ENABLED: bool = _resolve_network_features_enabled(_CONFIG)
INCLUDE_LICENSING: bool = _resolve_include_licensing(_CONFIG, NETWORK_FEATURES_ENABLED)

# Convenience for callers that want the reverse phrasing.
OFFLINE_MODE: bool = not NETWORK_FEATURES_ENABLED

# Tenant identity baked into this binary. TENANT_ID is the PLAINTEXT name
# sent to the issueToken Cloud Function (so the server can look up the right
# Secret Manager entry). TENANT_KEY is the hash used as the RTDB path
# segment -- client + server compute it the same way, so a Firebase console
# snapshot never leaks customer identities.
#
# The hash function MUST stay in lockstep with:
#   - Cloud Functions: `tenantKeyFor()` in functions/index.js
#   - DesktopHub C#:   `BuildConfig.TenantKey` / `ComputeTenantKey()`
TENANT_ID: str = str(_CONFIG.get("tenant_id", "ces")).strip().lower() or "ces"


def _compute_tenant_key(plaintext: str) -> str:
    """Deterministic 16-hex hash of the plaintext tenant name.

    SHA-256("dh-tenant-v1:" + lowercase(plaintext))[:16hex]
    """
    normalized = (plaintext or "").strip().lower()
    digest = hashlib.sha256(("dh-tenant-v1:" + normalized).encode("utf-8")).hexdigest()
    return digest[:16]


TENANT_KEY: str = _compute_tenant_key(TENANT_ID)


def why_offline() -> str | None:
    """Human-readable reason we are in offline mode, or None if we aren't.

    Used by the UI status pill to tell the user WHY they see 'Offline Build'.
    """
    if NETWORK_FEATURES_ENABLED:
        return None
    if _runtime_override_offline():
        return "SPEC_UPDATER_OFFLINE environment variable is set"
    return "Offline build (network features disabled at compile time)"


# Back-compat: older code imported `should_include_licensing` as a function.
def should_include_licensing() -> bool:
    return INCLUDE_LICENSING
