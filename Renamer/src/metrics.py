"""Device identification and fingerprinting utilities."""
import json
import uuid
import hashlib
import platform
from datetime import datetime, timezone
from pathlib import Path
from typing import Optional, Dict, Any
import os

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


def get_device_fingerprint() -> Dict[str, Any]:
    """Get comprehensive device identification."""
    return {
        'mac_address': MAC_ADDRESS,
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

