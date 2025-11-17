"""Subscription management for the Spec Header Date Updater with Firebase."""
import json
import os
import uuid
import secrets
import string
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Optional, Dict, Any, List
import hashlib

# Device identification utilities
try:
    from src.metrics import get_device_fingerprint, get_user_identifier, MAC_ADDRESS
    DEVICE_ID_AVAILABLE = True
except ImportError:
    DEVICE_ID_AVAILABLE = False
    MAC_ADDRESS = None
    def get_device_fingerprint():
        return {}
    def get_user_identifier(device_id, license_key=None):
        return device_id[:16]

# Firebase imports
try:
    import firebase_admin
    from firebase_admin import credentials, db as firebase_db, auth
    FIREBASE_AVAILABLE = True
    PYREBASE_AVAILABLE = False
    
    # Try importing pyrebase (optional, we can work without it)
    try:
        import pyrebase
        PYREBASE_AVAILABLE = True
    except Exception as e:
        print(f"Note: Pyrebase not available ({e}), using firebase-admin only (this is fine)")
        
except ImportError as e:
    FIREBASE_AVAILABLE = False
    PYREBASE_AVAILABLE = False
    print(f"Warning: Firebase import failed: {e}")
    print("Run: pip install firebase-admin")
except Exception as e:
    FIREBASE_AVAILABLE = False
    PYREBASE_AVAILABLE = False
    print(f"Error importing Firebase libraries: {type(e).__name__}: {e}")
    print("This may indicate a configuration issue, not a missing package.")


class SubscriptionManager:
    """Manages subscription status and validation with Firebase backend."""
    
    def __init__(self, app_id: str = "spec-updater", app_data_dir: Optional[Path] = None, config_path: Optional[Path] = None):
        """Initialize subscription manager.
        
        Args:
            app_id: Application identifier (e.g., 'spec-updater', 'coffee-stock-widget')
            app_data_dir: Directory to store subscription data. If None, uses system app data dir.
            config_path: Path to firebase_config.json. If None, looks in current directory.
        """
        if not FIREBASE_AVAILABLE:
            raise ImportError("Firebase libraries not installed. Run: pip install firebase-admin pyrebase4")
        
        self.app_id = app_id
        
        if app_data_dir is None:
            if os.name == 'nt':  # Windows
                app_data_dir = Path(os.getenv('LOCALAPPDATA', '~')).expanduser() / 'SpecHeaderUpdater'
            else:  # macOS/Linux
                app_data_dir = Path('~').expanduser() / '.config' / 'specheadupdater'
        
        self.app_data_dir = app_data_dir
        self.subscription_file = app_data_dir / f'subscription_{app_id}.json'
        self.device_id = self._get_device_id()
        self._subscription_data: Optional[Dict[str, Any]] = None
        self.device_fingerprint = get_device_fingerprint() if DEVICE_ID_AVAILABLE else {}
        
        # Create app data directory if it doesn't exist
        self.app_data_dir.mkdir(parents=True, exist_ok=True)
        
        # Initialize Firebase
        if config_path is None:
            # Check multiple locations for firebase_config.json
            config_locations = [
                Path(__file__).parent.parent / 'firebase_config.json',  # Renamer root folder
                Path(__file__).parent / 'firebase_config.json',  # src folder (fallback)
                Path(__file__).parent.parent.parent / 'firebase_config.json',  # desktop-widgets folder
            ]
            
            config_path = None
            for path in config_locations:
                if path.exists():
                    config_path = path
                    break
            
            if not config_path:
                # If no config found, use default path for better error message
                config_path = Path(__file__).parent.parent / 'firebase_config.json'
        
        self.firebase = None
        self.db = None
        self.auth_user = None
        self._init_firebase(config_path)
    
    def _init_firebase(self, config_path: Path) -> None:
        """Initialize Firebase connection."""
        try:
            # Load Firebase config
            if not config_path.exists():
                raise FileNotFoundError(
                    f"Firebase config not found at {config_path}. "
                    "Please create firebase_config.json - see FIREBASE_SETUP.md"
                )
            
            with open(config_path, 'r') as f:
                config = json.load(f)
            
            # Check if we should use pyrebase or firebase-admin
            if PYREBASE_AVAILABLE:
                # Use Pyrebase for client-side operations
                self.firebase = pyrebase.initialize_app(config)
                self.db = self.firebase.database()
                
                # Authenticate anonymously
                auth_client = self.firebase.auth()
                self.auth_user = auth_client.sign_in_anonymous()
                self.use_admin_sdk = False
            else:
                # Use Firebase Admin SDK directly (requires admin key)
                admin_key_locations = [
                    Path(__file__).parent.parent / 'firebase-admin-key.json',  # Renamer root folder
                    Path(__file__).parent / 'firebase-admin-key.json',  # src folder (fallback)
                    Path(__file__).parent.parent.parent / 'firebase-admin-key.json',  # desktop-widgets folder
                ]
                
                admin_key_path = None
                for path in admin_key_locations:
                    if path.exists():
                        admin_key_path = path
                        break
                
                if not admin_key_path:
                    raise FileNotFoundError(
                        "firebase-admin-key.json not found. "
                        "Required when pyrebase is not available."
                    )
                
                # Initialize Firebase Admin SDK if not already initialized
                if not firebase_admin._apps:
                    cred = credentials.Certificate(str(admin_key_path))
                    firebase_admin.initialize_app(cred, {
                        'databaseURL': config.get('databaseURL')
                    })
                
                self.db = firebase_db.reference()
                self.firebase = None
                self.auth_user = None
                self.use_admin_sdk = True
            
        except Exception as e:
            print(f"Error initializing Firebase: {e}")
            raise
    
    def _get_device_id(self) -> str:
        """Get or create a unique device ID."""
        # Ensure app data directory exists
        self.app_data_dir.mkdir(parents=True, exist_ok=True)
        
        id_file = self.app_data_dir / 'device_id.txt'
        if id_file.exists():
            return id_file.read_text().strip()
        
        # Generate new device ID
        device_id = str(uuid.uuid4())
        id_file.write_text(device_id)
        return device_id
    
    def _hash_license_key(self, license_key: str) -> str:
        """Create a secure hash of the license key for storage."""
        return hashlib.sha256(license_key.encode()).hexdigest()
    
    def _load_subscription(self) -> Optional[Dict[str, Any]]:
        """Load subscription data from local cache."""
        if self._subscription_data is not None:
            return self._subscription_data
            
        if not self.subscription_file.exists():
            return None
            
        try:
            with open(self.subscription_file, 'r') as f:
                self._subscription_data = json.load(f)
                return self._subscription_data
        except (json.JSONDecodeError, IOError):
            return None
    
    def _save_subscription(self, data: Dict[str, Any]) -> None:
        """Save subscription data to local cache."""
        try:
            # Ensure directory exists
            self.app_data_dir.mkdir(parents=True, exist_ok=True)
            
            with open(self.subscription_file, 'w') as f:
                json.dump(data, f, indent=2)
            self._subscription_data = data
        except IOError as e:
            print(f"Error saving subscription: {e}")
    
    def ensure_license_exists(self) -> bool:
        """Ensure user has a license (auto-create free license if needed)."""
        sub = self._load_subscription()
        if sub and sub.get('license_key'):
            # License already exists
            return True
        
        # No license - auto-create free license
        return self._create_free_license()
    
    def _create_free_license(self) -> bool:
        """Auto-create a free license for license management."""
        try:
            # Generate license key
            license_key = self._generate_free_license_key()
            
            # Create license in Firebase
            license_data = {
                'license_key': license_key,
                'app_id': self.app_id,
                'plan': 'free',
                'tier': 'free',
                'status': 'active',
                'source': 'auto-created',
                'created_at': datetime.now(timezone.utc).isoformat(),
                'expires_at': None,  # Free licenses never expire
                'max_devices': -1,   # Unlimited for free
                'documents_limit': 0,  # 0 = unlimited for free tier
                'documents_used': 0,
                'is_bundle': False,
                'email': None  # Can be added later
            }
            
            if self.use_admin_sdk:
                self.db.child('licenses').child(license_key).set(license_data)
            else:
                self.db.child('licenses').child(license_key).set(
                    license_data,
                    token=self.auth_user['idToken']
                )
            
            # Save locally
            self._save_subscription({
                'license_key': license_key,
                'expiry_date': None,
                'plan': 'free',
                'documents_remaining': -1,  # Unlimited
                'documents_limit': 0,
                'last_validated': datetime.now(timezone.utc).isoformat(),
                'source': 'auto-created'
            })
            
            # Register device
            self._register_device(license_key)
            
            return True
            
        except Exception as e:
            print(f"Error creating free license: {e}")
            import traceback
            traceback.print_exc()
            return False
    
    def _generate_free_license_key(self) -> str:
        """Generate license key for free license."""
        # Use device ID as base for uniqueness
        device_hash = hashlib.md5(self.device_id.encode()).hexdigest()[:8].upper()
        chars = string.ascii_uppercase + string.digits
        suffix = ''.join(secrets.choice(chars) for _ in range(8))
        return f"FREE-{device_hash}-{suffix}"
    
    def _register_device(self, license_key: str) -> None:
        """Register device activation."""
        activation_data = {
            'app_id': self.app_id,
            'license_key': license_key,
            'device_id': self.device_id,  # Store local device_id in data
            'device_name': os.environ.get('COMPUTERNAME', os.environ.get('HOSTNAME', 'Unknown')),
            'activated_at': datetime.now(timezone.utc).isoformat(),
            'last_validated': datetime.now(timezone.utc).isoformat(),
            'app_version': '1.0.0'
        }
        
        if self.use_admin_sdk:
            # Use local device_id as path with admin SDK
            self.db.child('device_activations').child(self.device_id).set(activation_data)
        else:
            # Use Firebase auth UID as path (required by security rules)
            # Store local device_id in the data for reference
            # Pyrebase returns user info with 'localId' or we decode from idToken
            auth_uid = self.auth_user.get('localId') or self.auth_user.get('userId')
            
            if not auth_uid:
                # Decode JWT token to get user_id (fallback)
                try:
                    # idToken is a JWT, decode without verification to get user_id
                    import base64
                    parts = self.auth_user['idToken'].split('.')
                    if len(parts) >= 2:
                        payload = parts[1]
                        # Add padding if needed
                        padding = 4 - len(payload) % 4
                        if padding != 4:
                            payload += '=' * padding
                        decoded = json.loads(base64.urlsafe_b64decode(payload))
                        auth_uid = decoded.get('user_id') or decoded.get('sub')
                except Exception as e:
                    print(f"Warning: Could not decode auth token: {e}")
            
            if not auth_uid:
                raise ValueError("Could not get auth UID from Firebase authentication")
            
            self.db.child('device_activations').child(auth_uid).set(
                activation_data,
                token=self.auth_user['idToken']
            )
    
    def is_subscribed(self) -> bool:
        """Check if the user has an active subscription (free or paid)."""
        sub = self._load_subscription()
        if not sub:
            # Try to auto-create free license
            if self.ensure_license_exists():
                sub = self._load_subscription()
            else:
                return False
        
        if not sub or not sub.get('license_key'):
            return False
        
        # Check if subscription is expired (only for paid licenses)
        expiry_str = sub.get('expiry_date')
        if expiry_str:  # Only paid licenses have expiration
            try:
                expiry = datetime.fromisoformat(expiry_str)
                return datetime.now(timezone.utc) < expiry
            except (ValueError, TypeError):
                return True  # If can't parse, assume valid
        
        # Free licenses or no expiration = always valid
        return True
    
    def get_subscription_info(self) -> Dict[str, Any]:
        """Get subscription information."""
        if not self.is_subscribed():
            return {
                'status': 'inactive',
                'expiry_date': None,
                'plan': None,
                'documents_remaining': 0,
                'documents_limit': 0
            }
            
        sub = self._load_subscription()
        return {
            'status': 'active',
            'expiry_date': sub.get('expiry_date'),
            'plan': sub.get('plan', 'free'),
            'documents_remaining': sub.get('documents_remaining', 0),
            'documents_limit': sub.get('documents_limit', 0)
        }
    
    def validate_license_key(self, license_key: str) -> bool:
        """Validate a license key with Firebase."""
        try:
            # Clean up the license key (remove spaces, convert to uppercase)
            license_key = license_key.strip().upper().replace(' ', '')
            
            # Query Firestore for the license
            # Note: Using Pyrebase for Realtime Database or Firestore REST API
            # For Firestore, we need to use the REST API directly
            from firebase_admin import firestore as admin_firestore
            
            # This requires admin initialization, so we'll use a different approach
            # We'll store a hash of the key locally and validate against Firebase
            
            # Get license data from Firebase Realtime Database
            try:
                if self.use_admin_sdk:
                    # Using Firebase Admin SDK
                    license_data = self.db.child('licenses').child(license_key).get()
                else:
                    # Using Pyrebase
                    license_data = self.db.child('licenses').child(license_key).get(
                        token=self.auth_user['idToken']
                    ).val()
            except Exception as e:
                print(f"Error querying license: {e}")
                return False
            
            if not license_data:
                # Try bundle license lookup - check for license_key-app_id format
                if self.use_admin_sdk:
                    all_licenses = self.db.child('licenses').get() or {}
                    for key, data in all_licenses.items():
                        # Check if this is a bundle license entry for this app
                        if (data.get('license_key') == license_key or 
                            data.get('bundle_parent') == license_key):
                            if data.get('app_id') == self.app_id:
                                license_data = data
                                break
                else:
                    # Using Pyrebase - query by bundle_parent
                    try:
                        bundle_licenses = self.db.child('licenses').order_by_child('bundle_parent').equal_to(license_key).get(
                            token=self.auth_user['idToken']
                        ).val() or {}
                        for key, data in bundle_licenses.items():
                            if data.get('app_id') == self.app_id:
                                license_data = data
                                break
                    except:
                        pass
                
                if not license_data:
                    print(f"License key not found")
                    return False
            
            # Validate app_id matches OR it's a bundle license
            license_app_id = license_data.get('app_id')
            is_bundle = license_data.get('is_bundle', False) or license_data.get('bundle_parent')
            
            if license_app_id != self.app_id and not is_bundle:
                # If it's a bundle, check if this app is included
                if not (is_bundle and license_app_id in [self.app_id, 'bundle']):
                    print(f"License is for different application: {license_app_id}")
                    return False
            
            # Validate license status
            if license_data.get('status') != 'active':
                print(f"License is not active: {license_data.get('status')}")
                return False
            
            # Check expiration
            expires_at = license_data.get('expires_at')
            if expires_at:
                # Firebase timestamp format
                try:
                    expiry_date = datetime.fromisoformat(expires_at.replace('Z', '+00:00'))
                    if datetime.now(timezone.utc) > expiry_date:
                        print("License has expired")
                        return False
                except (ValueError, AttributeError):
                    print("Invalid expiration date format")
                    return False
            
            # Check device limit
            max_devices = license_data.get('max_devices', 1)
            if max_devices > 0:  # -1 means unlimited
                # Check how many devices are using this license
                if self.use_admin_sdk:
                    # Using Admin SDK - query all activations and filter
                    all_activations = self.db.child('device_activations').get() or {}
                    device_activations = {
                        k: v for k, v in all_activations.items() 
                        if isinstance(v, dict) and v.get('license_key') == license_key
                    }
                else:
                    # Using Pyrebase
                    device_activations = self.db.child('device_activations').order_by_child('license_key').equal_to(license_key).get(
                        token=self.auth_user['idToken']
                    ).val() or {}
                
                if device_activations:
                    active_devices = len(device_activations)
                    
                    # Check if this device is already activated
                    device_already_activated = False
                    for device_id, activation in device_activations.items():
                        if device_id == self.device_id:
                            device_already_activated = True
                            break
                    
                    if not device_already_activated and active_devices >= max_devices:
                        print(f"Maximum device limit reached ({max_devices})")
                        return False
            
            # Register this device activation
            activation_data = {
                'app_id': self.app_id,
                'license_key': license_key,
                'device_name': os.environ.get('COMPUTERNAME', os.environ.get('HOSTNAME', 'Unknown')),
                'activated_at': datetime.utcnow().isoformat(),
                'last_validated': datetime.now(timezone.utc).isoformat(),
                'app_version': '1.0.0'
            }
            
            if self.use_admin_sdk:
                self.db.child('device_activations').child(self.device_id).set(activation_data)
            else:
                self.db.child('device_activations').child(self.device_id).set(
                    activation_data, 
                    token=self.auth_user['idToken']
                )
            
            # Save the subscription data locally
            self._save_subscription({
                'license_key': license_key,
                'expiry_date': license_data.get('expires_at'),
                'plan': license_data.get('plan', 'premium'),
                'documents_remaining': license_data.get('documents_limit', -1) - license_data.get('documents_used', 0),
                'documents_limit': license_data.get('documents_limit', -1),
                'last_validated': datetime.utcnow().isoformat()
            })
            
            return True
            
        except Exception as e:
            print(f"Error validating license: {e}")
            import traceback
            traceback.print_exc()
            return False
    
    def check_document_limit(self, requested_count: int = 1) -> Dict[str, Any]:
        """
        Check if the user can process more documents (server-side validation).
        
        Returns:
            Dict with 'allowed', 'remaining', 'limit', 'reason'
        """
        if not self.is_subscribed():
            return {
                'allowed': False,
                'remaining': 0,
                'limit': 0,
                'reason': 'No active license'
            }
        
        sub = self._load_subscription()
        if not sub or not sub.get('license_key'):
            return {
                'allowed': False,
                'remaining': 0,
                'limit': 0,
                'reason': 'License not found'
            }
        
        license_key = sub['license_key']
        
        # Get current usage from Firebase (server-side check)
        try:
            if self.use_admin_sdk:
                license_data = self.db.child('licenses').child(license_key).get()
                if not license_data:
                    # Try bundle lookup
                    all_licenses = self.db.child('licenses').get() or {}
                    for key, data in all_licenses.items():
                        if (data.get('license_key') == license_key or 
                            data.get('bundle_parent') == license_key):
                            if data.get('app_id') == self.app_id:
                                license_data = data
                                break
            else:
                license_data = self.db.child('licenses').child(license_key).get(
                    token=self.auth_user['idToken']
                ).val()
            
            if not license_data:
                return {
                    'allowed': False,
                    'remaining': 0,
                    'limit': 0,
                    'reason': 'License not found in database'
                }
            
            # Get server-side usage count
            documents_limit = license_data.get('documents_limit', -1)
            documents_used = license_data.get('documents_used', 0)
            
            # If limit is 0 or -1, unlimited
            if documents_limit <= 0:
                return {
                    'allowed': True,
                    'remaining': -1,  # Unlimited
                    'limit': documents_limit,
                    'reason': 'Unlimited'
                }
            
            # Calculate remaining
            remaining = max(0, documents_limit - documents_used)
            allowed = remaining >= requested_count
            
            return {
                'allowed': allowed,
                'remaining': remaining,
                'limit': documents_limit,
                'used': documents_used,
                'reason': 'Limit exceeded' if not allowed else 'Within limit'
            }
            
        except Exception as e:
            print(f"Error checking document limit: {e}")
            # On error, allow processing
            return {
                'allowed': True,
                'remaining': -1,
                'limit': -1,
                'reason': 'Limit check unavailable'
            }
    
    def check_document_limit_legacy(self) -> bool:
        """Legacy method for backward compatibility."""
        result = self.check_document_limit()
        return result.get('allowed', False)
    
    def record_document_processed(self, count: int = 1) -> bool:
        """
        Record that documents have been processed.
        Updates both usage_logs and server-side documents_used count.
        """
        # Ensure license exists (auto-create if needed)
        if not self.is_subscribed():
            if not self.ensure_license_exists():
                return False
        
        sub = self._load_subscription()
        if not sub or not sub.get('license_key'):
            return False
        
        license_key = sub.get('license_key')
        
        # Sync usage count to server
        try:
            sync_data = {
                'app_id': self.app_id,
                'device_id': self.device_id,
                'license_key': license_key,
                'documents_processed': count,
                'timestamp': datetime.now(timezone.utc).isoformat(),
                'app_version': '1.0.0'
            }
            
            if self.use_admin_sdk:
                self.db.child('usage_logs').push(sync_data)
            else:
                self.db.child('usage_logs').push(sync_data, token=self.auth_user['idToken'])
        except Exception:
            pass  # Silent fail - don't interrupt user experience
        
        # Update server-side documents_used count atomically
        try:
            if self.use_admin_sdk:
                # Get current count
                license_ref = self.db.child('licenses').child(license_key)
                license_data = license_ref.get()
                
                if license_data:
                    current_used = license_data.get('documents_used', 0)
                    documents_limit = license_data.get('documents_limit', -1)
                    
                    # Only update if not unlimited
                    if documents_limit > 0:
                        new_used = current_used + count
                        license_ref.update({
                            'documents_used': new_used,
                            'last_active': datetime.now(timezone.utc).isoformat()
                        })
                    else:
                        # Just update last_active for unlimited
                        license_ref.update({
                            'last_active': datetime.now(timezone.utc).isoformat()
                        })
            else:
                # Using Pyrebase - similar logic
                license_ref = self.db.child('licenses').child(license_key)
                license_data = license_ref.get(token=self.auth_user['idToken']).val()
                
                if license_data:
                    current_used = license_data.get('documents_used', 0)
                    documents_limit = license_data.get('documents_limit', -1)
                    
                    if documents_limit > 0:
                        new_used = current_used + count
                        license_ref.update({
                            'documents_used': new_used,
                            'last_active': datetime.now(timezone.utc).isoformat()
                        }, token=self.auth_user['idToken'])
                    else:
                        license_ref.update({
                            'last_active': datetime.now(timezone.utc).isoformat()
                        }, token=self.auth_user['idToken'])
        except Exception as e:
            print(f"Error updating server-side usage count: {e}")
            # Continue anyway - at least we logged it
        
        # Update local cache
        remaining = sub.get('documents_remaining')
        if remaining is not None and remaining >= 0:
            new_remaining = max(0, remaining - count)
            sub['documents_remaining'] = new_remaining
            self._save_subscription(sub)
        
        return True
    
    def _sync_activation_status(self) -> bool:
        """Sync license activation status with server."""
        try:
            sub = self._load_subscription()
            license_key = sub.get('license_key') if sub else None
            
            # Generate stable user identifier for license management
            user_id = get_user_identifier(self.device_id, license_key) if DEVICE_ID_AVAILABLE else self.device_id
            
            sync_data = {
                'app_id': self.app_id,
                'device_id': self.device_id,
                'user_id': user_id,
                'license_key': license_key,
                'mac_address': MAC_ADDRESS if DEVICE_ID_AVAILABLE else None,
                'device_info': self.device_fingerprint,
                'timestamp': datetime.now(timezone.utc).isoformat(),
                'app_version': '1.0.0',
                'event_type': 'app_launch'
            }
            
            if self.use_admin_sdk:
                self.db.child('app_launches').push(sync_data)
            else:
                self.db.child('app_launches').push(sync_data, token=self.auth_user['idToken'])
            
            return True
        except Exception:
            return False  # Silent fail
    
    def _update_license_usage(self, usage_data: Dict[str, Any]) -> bool:
        """
        Update license usage statistics on server.
        
        Args:
            usage_data: Dict containing usage statistics
        """
        try:
            sub = self._load_subscription()
            license_key = sub.get('license_key') if sub else None
            user_id = get_user_identifier(self.device_id, license_key) if DEVICE_ID_AVAILABLE else self.device_id
            
            update_data = {
                'app_id': self.app_id,
                'device_id': self.device_id,
                'user_id': user_id,
                'license_key': license_key,
                'mac_address': MAC_ADDRESS if DEVICE_ID_AVAILABLE else None,
                'device_info': self.device_fingerprint,
                'timestamp': datetime.now(timezone.utc).isoformat(),
                'app_version': '1.0.0',
                'event_type': 'processing_session',
                **usage_data
            }
            
            if self.use_admin_sdk:
                self.db.child('processing_sessions').push(update_data)
            else:
                self.db.child('processing_sessions').push(update_data, token=self.auth_user['idToken'])
            
            return True
        except Exception:
            return False  # Silent fail
    
    def refresh_subscription(self) -> bool:
        """Refresh subscription status from the server."""
        sub = self._load_subscription()
        if not sub or 'license_key' not in sub:
            return False
            
        return self.validate_license_key(sub['license_key'])
    
    def reset_subscription(self) -> None:
        """Remove subscription data (e.g., for logout)."""
        try:
            if self.subscription_file.exists():
                self.subscription_file.unlink()
            self._subscription_data = None
        except OSError as e:
            print(f"Error resetting subscription: {e}")
