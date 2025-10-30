"""Subscription management for the Spec Header Date Updater with Firebase."""
import json
import os
import uuid
import secrets
import string
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional, Dict, Any
import hashlib

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
    
    def is_subscribed(self) -> bool:
        """Check if the user has an active subscription."""
        sub = self._load_subscription()
        if not sub:
            return False
            
        # Check if subscription is expired
        expiry_str = sub.get('expiry_date')
        if not expiry_str:
            return False
            
        try:
            expiry = datetime.fromisoformat(expiry_str)
            return datetime.utcnow() < expiry
        except (ValueError, TypeError):
            return False
    
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
                print(f"License key not found")
                return False
            
            # Validate app_id matches
            if license_data.get('app_id') != self.app_id:
                print(f"License is for different application: {license_data.get('app_id')}")
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
                    if datetime.utcnow() > expiry_date:
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
                'last_validated': datetime.utcnow().isoformat(),
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
    
    def check_document_limit(self) -> bool:
        """Check if the user can process more documents."""
        if not self.is_subscribed():
            return False
            
        sub = self._load_subscription()
        if not sub:
            return False
            
        # If documents_remaining is None or negative, assume unlimited
        remaining = sub.get('documents_remaining')
        if remaining is None or remaining < 0:
            return True
            
        return remaining > 0
    
    def record_document_processed(self, count: int = 1) -> bool:
        """Record that documents have been processed."""
        if not self.is_subscribed():
            return False
            
        sub = self._load_subscription()
        if not sub:
            return False
            
        # Log usage to Firebase
        try:
            log_data = {
                'app_id': self.app_id,
                'device_id': self.device_id,
                'license_key': sub.get('license_key'),
                'documents_processed': count,
                'timestamp': datetime.utcnow().isoformat(),
                'app_version': '1.0.0'
            }
            
            if self.use_admin_sdk:
                self.db.child('usage_logs').push(log_data)
            else:
                self.db.child('usage_logs').push(log_data, token=self.auth_user['idToken'])
        except Exception as e:
            print(f"Error logging usage: {e}")
        
        # If documents_remaining is None or -1, assume unlimited
        remaining = sub.get('documents_remaining')
        if remaining is None or remaining < 0:
            return True
            
        # Update remaining count locally
        new_remaining = max(0, remaining - count)
        sub['documents_remaining'] = new_remaining
        
        # Save the updated subscription
        self._save_subscription(sub)
        return True
    
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
