"""
Embedded Firebase configuration.

This file contains the Firebase configuration embedded directly in the source code,
so it doesn't need to be shipped as a separate JSON file alongside the executable.

IMPORTANT: This file should NOT contain any admin credentials or service account keys.
Only client-side Firebase config (which is safe to include in client apps).
"""

# Firebase client configuration (safe to embed - this is public config)
FIREBASE_CONFIG = {
    "apiKey": "AIzaSyBTmqZ6HMdKEoR8bMtSka-gUm_XiqwLYHM",
    "authDomain": "licenses-ff136.firebaseapp.com",
    "projectId": "licenses-ff136",
    "storageBucket": "licenses-ff136.firebasestorage.app",
    "databaseURL": "https://licenses-ff136-default-rtdb.firebaseio.com",
    "messagingSenderId": "790385090879",
    "appId": "1:790385090879:web:8cc605a73fcf563406b38a",
    "measurementId": "G-RB2X3HM8JP"
}


def get_firebase_config() -> dict:
    """Return the embedded Firebase configuration."""
    return FIREBASE_CONFIG.copy()
