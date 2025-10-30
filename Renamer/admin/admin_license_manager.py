#!/usr/bin/env python3
"""
Admin tool for managing licenses in Firebase.
Requires firebase-admin-key.json to be present.

Command-line tool for managing licenses in Firebase.
Requires Firebase Admin SDK credentials.
"""

import argparse
import secrets
import string
import json
import sys
from datetime import datetime, timedelta
from pathlib import Path
from typing import Optional

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

try:
    import firebase_admin
    from firebase_admin import credentials, db
except ImportError:
    print("Error: firebase-admin not installed.")
    print("Run: pip install firebase-admin")
    exit(1)


class LicenseManager:
    """Manages license keys in Firebase."""
    
    def __init__(self, admin_key_path, database_url: str):
        """Initialize the license manager with admin credentials."""
        # Convert to Path if string
        if isinstance(admin_key_path, str):
            admin_key_path = Path(admin_key_path)
        
        if not admin_key_path.exists():
            raise FileNotFoundError(
                f"Admin key file not found: {admin_key_path}\n"
                "Please download the service account key from Firebase Console."
            )
        
        # Initialize Firebase Admin SDK
        cred = credentials.Certificate(str(admin_key_path))
        firebase_admin.initialize_app(cred, {
            'databaseURL': database_url
        })
        
        self.licenses_ref = db.reference('licenses')
        self.activations_ref = db.reference('device_activations')
    
    @staticmethod
    def generate_license_key() -> str:
        """Generate a secure random license key."""
        # Use cryptographically secure random generation
        # Format: XXXXX-XXXXX-XXXXX-XXXXX (20 chars + 3 hyphens)
        chars = string.ascii_uppercase + string.digits
        parts = []
        for _ in range(4):
            part = ''.join(secrets.choice(chars) for _ in range(5))
            parts.append(part)
        return '-'.join(parts)
    
    def create_license(
        self, 
        email: str,
        app_id: str,
        plan: str = 'premium',
        duration_days: int = 365,
        max_devices: int = 3,
        documents_limit: int = -1
    ) -> str:
        """
        Create a new license.
        
        Args:
            email: Customer email
            app_id: Application identifier (e.g., 'spec-updater', 'coffee-stock-widget')
            plan: License plan (free, basic, premium, business)
            duration_days: Number of days the license is valid
            max_devices: Maximum number of devices (-1 for unlimited)
            documents_limit: Maximum documents per month (-1 for unlimited)
        
        Returns:
            The generated license key
        """
        license_key = self.generate_license_key()
        now = datetime.utcnow()
        expires_at = now + timedelta(days=duration_days)
        
        license_data = {
            'license_key': license_key,
            'app_id': app_id,
            'email': email,
            'plan': plan,
            'status': 'active',
            'created_at': now.isoformat(),
            'expires_at': expires_at.isoformat(),
            'max_devices': max_devices,
            'documents_limit': documents_limit,
            'documents_used': 0
        }
        
        # Save to Firebase
        self.licenses_ref.child(license_key).set(license_data)
        
        print(f"✅ License created successfully!")
        print(f"   License Key: {license_key}")
        print(f"   App ID: {app_id}")
        print(f"   Email: {email}")
        print(f"   Plan: {plan}")
        print(f"   Expires: {expires_at.strftime('%Y-%m-%d')}")
        print(f"   Max Devices: {'Unlimited' if max_devices < 0 else max_devices}")
        print(f"   Document Limit: {'Unlimited' if documents_limit < 0 else documents_limit}")
        
        return license_key
    
    def get_all_licenses(self, email: Optional[str] = None, status: Optional[str] = None, app_id: Optional[str] = None):
        """Get all licenses as a list of dictionaries, optionally filtered."""
        licenses = self.licenses_ref.get()
        
        if not licenses:
            return []
        
        # Filter and format licenses
        result = []
        for key, data in licenses.items():
            if email and data.get('email') != email:
                continue
            if status and data.get('status') != status:
                continue
            if app_id and data.get('app_id') != app_id:
                continue
            
            # Add license key to data
            license_info = dict(data)
            license_info['license_key'] = key
            result.append(license_info)
        
        return result
    
    def list_licenses(self, email: Optional[str] = None, status: Optional[str] = None, app_id: Optional[str] = None):
        """List all licenses, optionally filtered by email, status, or app_id."""
        licenses = self.licenses_ref.get()
        
        if not licenses:
            print("No licenses found.")
            return
        
        # Filter licenses
        filtered = []
        for key, data in licenses.items():
            if email and data.get('email') != email:
                continue
            if status and data.get('status') != status:
                continue
            if app_id and data.get('app_id') != app_id:
                continue
            filtered.append((key, data))
        
        if not filtered:
            print(f"No licenses found matching filters.")
            return
        
        # Print licenses
        print(f"\nFound {len(filtered)} license(s):\n")
        for key, data in filtered:
            expires = datetime.fromisoformat(data.get('expires_at', ''))
            is_expired = datetime.utcnow() > expires
            
            print(f"License: {key}")
            print(f"  App ID: {data.get('app_id', 'N/A')}")
            print(f"  Email: {data.get('email')}")
            print(f"  Plan: {data.get('plan')}")
            print(f"  Status: {data.get('status')} {'(EXPIRED)' if is_expired else ''}")
            print(f"  Created: {data.get('created_at', 'N/A')}")
            print(f"  Expires: {expires.strftime('%Y-%m-%d')}")
            print(f"  Max Devices: {data.get('max_devices')}")
            print(f"  Documents: {data.get('documents_used', 0)} / {data.get('documents_limit', 0)}")
            print()
    
    def revoke_license(self, license_key: str):
        """Revoke a license."""
        license_data = self.licenses_ref.child(license_key).get()
        
        if not license_data:
            print(f"❌ License not found: {license_key}")
            return False
        
        # Update status to suspended
        self.licenses_ref.child(license_key).update({
            'status': 'suspended',
            'revoked_at': datetime.utcnow().isoformat()
        })
        
        print(f"✅ License revoked: {license_key}")
        return True
    
    def extend_license(self, license_key: str, days: int):
        """Extend a license expiration date."""
        license_data = self.licenses_ref.child(license_key).get()
        
        if not license_data:
            print(f"❌ License not found: {license_key}")
            return False
        
        current_expiry = datetime.fromisoformat(license_data['expires_at'])
        new_expiry = current_expiry + timedelta(days=days)
        
        self.licenses_ref.child(license_key).update({
            'expires_at': new_expiry.isoformat()
        })
        
        print(f"✅ License extended: {license_key}")
        print(f"   New expiration: {new_expiry.strftime('%Y-%m-%d')}")
        return True
    
    def get_license_info(self, license_key: str):
        """Get detailed information about a license."""
        license_data = self.licenses_ref.child(license_key).get()
        
        if not license_data:
            print(f"❌ License not found: {license_key}")
            return
        
        # Get active devices
        activations = self.activations_ref.get() or {}
        active_devices = []
        for device_id, activation in activations.items():
            if activation.get('license_key') == license_key:
                active_devices.append({
                    'device_id': device_id,
                    'device_name': activation.get('device_name'),
                    'activated_at': activation.get('activated_at'),
                    'last_validated': activation.get('last_validated')
                })
        
        # Print info
        print(f"\nLicense Information: {license_key}")
        print(f"  App ID: {license_data.get('app_id', 'N/A')}")
        print(f"  Email: {license_data.get('email')}")
        print(f"  Plan: {license_data.get('plan')}")
        print(f"  Status: {license_data.get('status')}")
        print(f"  Created: {license_data.get('created_at')}")
        print(f"  Expires: {license_data.get('expires_at')}")
        print(f"  Max Devices: {license_data.get('max_devices')}")
        print(f"  Documents Used: {license_data.get('documents_used', 0)} / {license_data.get('documents_limit', 0)}")
        print(f"\n  Active Devices ({len(active_devices)}):")
        for device in active_devices:
            print(f"    - {device['device_name']} ({device['device_id'][:8]}...)")
            print(f"      Activated: {device['activated_at']}")
            print(f"      Last Seen: {device['last_validated']}")


def main():
    parser = argparse.ArgumentParser(description='Manage license keys in Firebase')
    subparsers = parser.add_subparsers(dest='command', help='Command to execute')
    
    # Create license command
    create_parser = subparsers.add_parser('create', help='Create a new license')
    create_parser.add_argument('--email', required=True, help='Customer email')
    create_parser.add_argument('--app-id', required=True, help='Application ID (e.g., spec-updater, coffee-stock-widget)')
    create_parser.add_argument('--plan', default='premium', choices=['free', 'basic', 'premium', 'business'])
    create_parser.add_argument('--duration', type=int, default=365, help='Duration in days')
    create_parser.add_argument('--max-devices', type=int, default=3, help='Max devices (-1 for unlimited)')
    create_parser.add_argument('--documents-limit', type=int, default=-1, help='Monthly document limit (-1 for unlimited)')
    
    # List licenses command
    list_parser = subparsers.add_parser('list', help='List licenses')
    list_parser.add_argument('--email', help='Filter by email')
    list_parser.add_argument('--status', choices=['active', 'expired', 'suspended'], help='Filter by status')
    list_parser.add_argument('--app-id', help='Filter by application ID')
    
    # Revoke license command
    revoke_parser = subparsers.add_parser('revoke', help='Revoke a license')
    revoke_parser.add_argument('--license', required=True, help='License key to revoke')
    
    # Extend license command
    extend_parser = subparsers.add_parser('extend', help='Extend license expiration')
    extend_parser.add_argument('--license', required=True, help='License key to extend')
    extend_parser.add_argument('--days', type=int, required=True, help='Number of days to extend')
    
    # Info command
    info_parser = subparsers.add_parser('info', help='Get license information')
    info_parser.add_argument('--license', required=True, help='License key')
    
    # Parse arguments
    args = parser.parse_args()
    
    if not args.command:
        parser.print_help()
        return
    
    # Check multiple locations for firebase_config.json
    config_locations = [
        Path(__file__).parent.parent / 'firebase_config.json',  # Renamer root folder
        Path(__file__).parent / 'firebase_config.json',  # admin folder (fallback)
        Path(__file__).parent.parent.parent / 'firebase_config.json',  # desktop-widgets folder
    ]
    
    config_path = None
    for path in config_locations:
        if path.exists():
            config_path = path
            break
    
    if not config_path:
        print(f"❌ Firebase config not found in any of these locations:")
        for loc in config_locations:
            print(f"   - {loc}")
        print("\nPlease create firebase_config.json in the Renamer root folder.")
        print("You can copy from config/firebase_config.example.json")
        return
    
    # Check multiple locations for admin key
    admin_key_locations = [
        Path(__file__).parent.parent / 'firebase-admin-key.json',  # Renamer root folder
        Path(__file__).parent / 'firebase-admin-key.json',  # admin folder (fallback)
        Path(__file__).parent.parent.parent / 'firebase-admin-key.json',  # desktop-widgets folder
    ]
    
    admin_key_path = None
    for path in admin_key_locations:
        if path.exists():
            admin_key_path = path
            break
    
    if not admin_key_path:
        print(f"❌ Admin key not found in any of these locations:")
        for loc in admin_key_locations:
            print(f"   - {loc}")
        print("\nPlease download firebase-admin-key.json from Firebase Console")
        print("and place it in the Renamer folder.")
        return
    
    with open(config_path) as f:
        config = json.load(f)
    
    database_url = config.get('databaseURL')
    if not database_url:
        print("❌ databaseURL not found in firebase_config.json")
        return
    
    # Initialize manager
    try:
        manager = LicenseManager(admin_key_path, database_url)
    except Exception as e:
        print(f"❌ Error initializing Firebase: {e}")
        return
    
    # Execute command
    try:
        if args.command == 'create':
            manager.create_license(
                email=args.email,
                app_id=args.app_id,
                plan=args.plan,
                duration_days=args.duration,
                max_devices=args.max_devices,
                documents_limit=args.documents_limit
            )
        elif args.command == 'list':
            manager.list_licenses(
                email=args.email, 
                status=args.status,
                app_id=getattr(args, 'app_id', None)
            )
        elif args.command == 'revoke':
            manager.revoke_license(args.license)
        elif args.command == 'extend':
            manager.extend_license(args.license, args.days)
        elif args.command == 'info':
            manager.get_license_info(args.license)
    except Exception as e:
        print(f"❌ Error: {e}")
        import traceback
        traceback.print_exc()


if __name__ == '__main__':
    main()
