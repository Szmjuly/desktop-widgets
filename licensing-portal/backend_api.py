"""
Backend API for Licensing Portal
Connects the web portal to Firebase for license creation

This is a Flask example - you can adapt to FastAPI, Express.js, etc.
"""

from flask import Flask, request, jsonify
import firebase_admin
from firebase_admin import credentials, db
import secrets
import string
from datetime import datetime, timedelta
import os

app = Flask(__name__)

# Initialize Firebase Admin
# You'll need to provide path to firebase-admin-key.json
FIREBASE_ADMIN_KEY = os.getenv('FIREBASE_ADMIN_KEY_PATH', 'firebase-admin-key.json')
FIREBASE_DB_URL = os.getenv('FIREBASE_DATABASE_URL', 'https://your-project.firebaseio.com')

cred = credentials.Certificate(FIREBASE_ADMIN_KEY)
firebase_admin.initialize_app(cred, {
    'databaseURL': FIREBASE_DB_URL
})

# Plan configurations
PLAN_CONFIG = {
    'spec-updater': {
        'basic': {'max_devices': 2, 'documents_limit': 100, 'price_cents': 999},
        'premium': {'max_devices': 5, 'documents_limit': -1, 'price_cents': 1999},
        'business': {'max_devices': -1, 'documents_limit': -1, 'price_cents': 4999}
    },
    'bundle': {
        'basic': {'max_devices': 3, 'documents_limit': 100, 'price_cents': 2499},
        'premium': {'max_devices': 5, 'documents_limit': -1, 'price_cents': 3999},
        'business': {'max_devices': -1, 'documents_limit': -1, 'price_cents': 7999}
    }
}

# Bundle apps (current + future)
BUNDLE_APPS = ['spec-updater', 'coffee-stock-widget']


def generate_license_key():
    """Generate license key in format: XXXXX-XXXXX-XXXXX-XXXXX"""
    chars = string.ascii_uppercase + string.digits
    parts = []
    for _ in range(4):
        part = ''.join(secrets.choice(chars) for _ in range(5))
        parts.append(part)
    return '-'.join(parts)


@app.route('/api/process-payment', methods=['POST'])
def process_payment():
    """
    Process payment and create license.
    
    This endpoint should be called after payment is confirmed
    (via Stripe webhook, PayPal IPN, etc.)
    """
    try:
        data = request.json
        email = data['email']
        product = data['product']  # "spec-updater" or "bundle"
        plan = data['plan']  # "basic", "premium", "business"
        payment_id = data.get('paymentId')  # Stripe charge ID or PayPal transaction ID
        payment_method = data.get('paymentMethod', 'stripe')
        
        # Generate license key
        license_key = generate_license_key()
        
        # Calculate expiration (default: 1 year)
        duration_days = data.get('durationDays', 365)
        expires_at = (datetime.utcnow() + timedelta(days=duration_days)).isoformat()
        
        # Get plan configuration
        config = PLAN_CONFIG[product][plan]
        
        # Create license(s) in Firebase
        db_ref = db.reference('licenses')
        
        if product == 'bundle':
            # Create license entry for each app in bundle
            for app_id in BUNDLE_APPS:
                license_data = {
                    'license_key': license_key,
                    'app_id': app_id,
                    'bundle_parent': license_key,
                    'email': email,
                    'plan': plan,
                    'tier': plan,
                    'status': 'active',
                    'created_at': datetime.utcnow().isoformat(),
                    'expires_at': expires_at,
                    'max_devices': config['max_devices'],
                    'documents_limit': config['documents_limit'],
                    'documents_used': 0,
                    'is_bundle': True,
                    'stripe_customer_id': payment_id if payment_method == 'stripe' else None,
                    'paypal_transaction_id': payment_id if payment_method == 'paypal' else None,
                    'payment_method': payment_method,
                    'purchased_product': 'bundle',
                    'purchased_tier': plan
                }
                # Use license_key-app_id as key to avoid conflicts
                db_ref.child(f'{license_key}-{app_id}').set(license_data)
        else:
            # Single app license
            license_data = {
                'license_key': license_key,
                'app_id': product,
                'email': email,
                'plan': plan,
                'tier': plan,
                'status': 'active',
                'created_at': datetime.utcnow().isoformat(),
                'expires_at': expires_at,
                'max_devices': config['max_devices'],
                'documents_limit': config['documents_limit'],
                'documents_used': 0,
                'is_bundle': False,
                'stripe_customer_id': payment_id if payment_method == 'stripe' else None,
                'paypal_transaction_id': payment_id if payment_method == 'paypal' else None,
                'payment_method': payment_method,
                'purchased_product': product,
                'purchased_tier': plan
            }
            db_ref.child(license_key).set(license_data)
        
        # TODO: Send email with license key
        # send_license_email(email, license_key, product, plan, expires_at)
        
        return jsonify({
            'success': True,
            'licenseKey': license_key,
            'expiresAt': expires_at,
            'transactionId': payment_id
        })
        
    except Exception as e:
        return jsonify({
            'success': False,
            'error': str(e)
        }), 500


@app.route('/api/create-license', methods=['POST'])
def create_license():
    """
    Admin endpoint to create license without payment.
    Useful for testing or manual license creation.
    """
    try:
        data = request.json
        email = data['email']
        product = data['product']
        plan = data['plan']
        license_key = data.get('licenseKey') or generate_license_key()
        duration_days = data.get('durationDays', 365)
        
        expires_at = (datetime.utcnow() + timedelta(days=duration_days)).isoformat()
        config = PLAN_CONFIG[product][plan]
        
        db_ref = db.reference('licenses')
        
        if product == 'bundle':
            for app_id in BUNDLE_APPS:
                license_data = {
                    'license_key': license_key,
                    'app_id': app_id,
                    'bundle_parent': license_key,
                    'email': email,
                    'plan': plan,
                    'status': 'active',
                    'created_at': datetime.utcnow().isoformat(),
                    'expires_at': expires_at,
                    'max_devices': config['max_devices'],
                    'documents_limit': config['documents_limit'],
                    'documents_used': 0,
                    'is_bundle': True
                }
                db_ref.child(f'{license_key}-{app_id}').set(license_data)
        else:
            license_data = {
                'license_key': license_key,
                'app_id': product,
                'email': email,
                'plan': plan,
                'status': 'active',
                'created_at': datetime.utcnow().isoformat(),
                'expires_at': expires_at,
                'max_devices': config['max_devices'],
                'documents_limit': config['documents_limit'],
                'documents_used': 0,
                'is_bundle': False
            }
            db_ref.child(license_key).set(license_data)
        
        return jsonify({
            'success': True,
            'licenseKey': license_key,
            'expiresAt': expires_at
        })
        
    except Exception as e:
        return jsonify({
            'success': False,
            'error': str(e)
        }), 500


@app.route('/api/lookup-license', methods=['POST'])
def lookup_license():
    """Look up license information for customer portal."""
    try:
        data = request.json
        license_key = data['licenseKey']
        email = data.get('email')
        
        db_ref = db.reference('licenses')
        
        # Try direct lookup first
        license_data = db_ref.child(license_key).get()
        
        # If not found, try bundle lookup
        if not license_data:
            all_licenses = db_ref.get() or {}
            for key, data in all_licenses.items():
                if data.get('license_key') == license_key or data.get('bundle_parent') == license_key:
                    if not email or data.get('email') == email:
                        license_data = data
                        break
        
        if not license_data:
            return jsonify({'success': False, 'error': 'License not found'}), 404
        
        # Verify email if provided
        if email and license_data.get('email') != email:
            return jsonify({'success': False, 'error': 'Email does not match'}), 403
        
        # Get device activations
        activations_ref = db.reference('device_activations')
        all_activations = activations_ref.get() or {}
        devices = []
        
        for device_id, activation in all_activations.items():
            if activation.get('license_key') == license_key:
                devices.append({
                    'device_id': device_id,
                    'device_name': activation.get('device_name'),
                    'app_id': activation.get('app_id'),
                    'activated_at': activation.get('activated_at'),
                    'last_validated': activation.get('last_validated')
                })
        
        return jsonify({
            'success': True,
            'license': {
                'license_key': license_key,
                'app_id': license_data.get('app_id'),
                'plan': license_data.get('plan'),
                'status': license_data.get('status'),
                'expires_at': license_data.get('expires_at'),
                'max_devices': license_data.get('max_devices'),
                'documents_limit': license_data.get('documents_limit'),
                'documents_used': license_data.get('documents_used', 0),
                'is_bundle': license_data.get('is_bundle', False)
            },
            'devices': devices
        })
        
    except Exception as e:
        return jsonify({
            'success': False,
            'error': str(e)
        }), 500


if __name__ == '__main__':
    # Run with: python backend_api.py
    app.run(debug=True, port=5000)

