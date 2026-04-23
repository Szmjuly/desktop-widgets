"""Device identification and fingerprinting utilities.

Note on PII hashing (2026-04-23):
The raw MAC address used to be included verbatim in outbound device-fingerprint
payloads. That matched the pattern DesktopHub v1.9 was EDR-flagged for. The
raw MAC is now kept LOCAL (used only for deriving the stable device-id hash);
any code that wants to send a MAC-derived identifier outbound must use
`get_mac_hash()`, which returns a salted, truncated SHA-256.
"""
import json
import uuid
import hashlib
import platform
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional, Dict, Any
import os


# Salt for hashing PII values before they leave the machine. Bumping the "v"
# intentionally invalidates previously-hashed values on the server side if
# we ever need to break linkage. The salt is NOT a secret -- hashes must be
# deterministic per-device so the admin dashboard can dedupe machines.
_PII_HASH_SALT = "SpecUpdater|pii|v1|2026"


def _hash_pii(raw: Optional[str]) -> str:
    """Salted SHA-256, truncated to 16 hex chars (64 bits).

    Returns "unknown" for falsy / sentinel inputs. Deterministic: the same
    raw value always maps to the same hash on the same build.
    """
    if not raw or str(raw).strip().lower() == "unknown":
        return "unknown"
    material = f"{_PII_HASH_SALT}|{str(raw).strip()}"
    return hashlib.sha256(material.encode("utf-8")).hexdigest()[:16]

# Try to get MAC address (cross-platform)
try:
    import uuid as uuid_lib
    MAC_ADDRESS = ':'.join(['{:02x}'.format((uuid_lib.getnode() >> elements) & 0xff) 
                            for elements in range(0, 2*6, 2)][::-1])
except Exception:
    MAC_ADDRESS = None

# Alternative method for Windows
if not MAC_ADDRESS and platform.system() == 'Windows':
    try:
        import subprocess
        result = subprocess.run(['getmac', '/fo', 'csv', '/nh'], 
                              capture_output=True, text=True, timeout=2)
        if result.returncode == 0:
            lines = result.stdout.strip().split('\n')
            if lines and ',' in lines[0]:
                MAC_ADDRESS = lines[0].split(',')[0].strip()
    except Exception:
        pass

# Alternative method for Linux/Mac
if not MAC_ADDRESS:
    try:
        import subprocess
        result = subprocess.run(['ifconfig'], capture_output=True, text=True, timeout=2)
        if result.returncode == 0:
            # Extract first MAC address
            import re
            mac_match = re.search(r'([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})', result.stdout)
            if mac_match:
                MAC_ADDRESS = mac_match.group(0)
    except Exception:
        pass

# Fallback: generate stable ID based on machine characteristics
if not MAC_ADDRESS:
    try:
        # Use machine info to create stable ID
        machine_info = f"{platform.node()}{platform.machine()}{platform.processor()}"
        MAC_ADDRESS = hashlib.md5(machine_info.encode()).hexdigest()[:12]
        MAC_ADDRESS = ':'.join([MAC_ADDRESS[i:i+2] for i in range(0, 12, 2)])
    except Exception:
        MAC_ADDRESS = "unknown"


def get_mac_hash() -> str:
    """Return the hashed form of the local MAC address (safe to send outbound)."""
    return _hash_pii(MAC_ADDRESS)


def get_device_fingerprint() -> Dict[str, Any]:
    """Device metadata safe to send outbound.

    The `mac_address` field here is the HASHED value -- the raw MAC never
    leaves this process. Consumers that need the raw MAC for a local-only
    purpose (deriving a stable device-id etc.) read the module-level
    `MAC_ADDRESS` directly.
    """
    return {
        'mac_address': get_mac_hash(),
        'machine_name': platform.node(),
        'platform': platform.system(),
        'platform_version': platform.version(),
        'machine': platform.machine(),
        'processor': platform.processor(),
        'python_version': platform.python_version()
    }


def get_user_identifier(device_id: str, license_key: Optional[str] = None) -> str:
    """
    Generate stable user identifier for license management.
    
    Identifies users across devices by combining:
    1. License key (if available) - links multiple devices to same user
    2. Device ID (UUID) - identifies device
    3. MAC address - helps identify same physical machine
    
    Returns a hash that represents this user/device combination.
    """
    components = [device_id]
    if license_key:
        components.append(license_key)
    if MAC_ADDRESS and MAC_ADDRESS != "unknown":
        components.append(MAC_ADDRESS)
    
    # Create stable hash
    combined = '|'.join(components)
    return hashlib.sha256(combined.encode()).hexdigest()[:16]

