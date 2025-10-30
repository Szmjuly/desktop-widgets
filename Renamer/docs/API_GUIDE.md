# License Creation API Guide

This guide explains how to create a secure API for remote license creation from other platforms (e.g., your website, payment processor, or business tools).

## 📋 Table of Contents

1. [Security Considerations](#security-considerations)
2. [Option 1: Flask REST API (Simple)](#option-1-flask-rest-api-simple)
3. [Option 2: Firebase Cloud Functions (Production)](#option-2-firebase-cloud-functions-production)
4. [Integration Examples](#integration-examples)

---

## Security Considerations

### ⚠️ Important Security Questions

**Q: Is it secure to expose license creation via API?**  
**A:** Yes, **IF** you implement proper authentication and authorization!

### Security Levels

| Method | Security | Use Case |
|--------|----------|----------|
| **No Auth** | ❌ Very Insecure | Never use |
| **API Key** | ⚠️ Basic | Internal tools only |
| **JWT + HTTPS** | ✅ Secure | Production (internal) |
| **Firebase Auth** | ✅✅ Very Secure | Production (public) |
| **OAuth 2.0** | ✅✅✅ Enterprise | Large scale |

### What to Protect

✅ **Must Have:**
- HTTPS only (never HTTP)
- Authentication (API key minimum)
- Rate limiting
- Input validation
- Audit logging

⚠️ **Should Have:**
- IP whitelisting (if internal)
- Request signing
- Timestamp validation
- Quota limits per client

---

## Option 1: Flask REST API (Simple)

**Best for:** Internal tools, webhooks from trusted sources (Stripe, PayPal, etc.)

### Setup

1. **Install Dependencies**
```bash
pip install flask flask-cors pyjwt
```

2. **Create API Server** (`license_api.py`)

```python
from flask import Flask, request, jsonify
from flask_cors import CORS
import jwt
import os
from datetime import datetime, timedelta
from pathlib import Path
import json
from admin_license_manager import LicenseManager

app = Flask(__name__)
CORS(app)  # Enable CORS for web requests

# Security configuration
API_SECRET = os.getenv('LICENSE_API_SECRET', 'CHANGE_THIS_SECRET_KEY')
API_KEYS = set(os.getenv('VALID_API_KEYS', 'key1,key2,key3').split(','))

# Initialize Firebase
config_path = Path(__file__).parent / 'firebase_config.json'
admin_key_path = Path(__file__).parent / 'firebase-admin-key.json'

with open(config_path) as f:
    config = json.load(f)

manager = LicenseManager(admin_key_path, config['databaseURL'])


def require_auth(f):
    """Decorator to require API key authentication."""
    def decorated(*args, **kwargs):
        # Check for API key in header
        api_key = request.headers.get('X-API-Key')
        
        if not api_key:
            return jsonify({'error': 'Missing API key'}), 401
        
        if api_key not in API_KEYS:
            return jsonify({'error': 'Invalid API key'}), 403
        
        return f(*args, **kwargs)
    
    decorated.__name__ = f.__name__
    return decorated


@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint."""
    return jsonify({'status': 'ok', 'timestamp': datetime.utcnow().isoformat()})


@app.route('/api/v1/licenses', methods=['POST'])
@require_auth
def create_license():
    """
    Create a new license.
    
    Request Body:
    {
        "email": "customer@example.com",
        "app_id": "spec-updater",
        "plan": "premium",
        "duration_days": 365,
        "max_devices": 3,
        "documents_limit": -1
    }
    """
    try:
        data = request.get_json()
        
        # Validate required fields
        required = ['email', 'app_id']
        for field in required:
            if field not in data:
                return jsonify({'error': f'Missing required field: {field}'}), 400
        
        # Create license
        license_key = manager.create_license(
            email=data['email'],
            app_id=data['app_id'],
            plan=data.get('plan', 'premium'),
            duration_days=data.get('duration_days', 365),
            max_devices=data.get('max_devices', 3),
            documents_limit=data.get('documents_limit', -1)
        )
        
        return jsonify({
            'success': True,
            'license_key': license_key,
            'created_at': datetime.utcnow().isoformat()
        }), 201
        
    except Exception as e:
        return jsonify({'error': str(e)}), 500


@app.route('/api/v1/licenses/<license_key>', methods=['GET'])
@require_auth
def get_license(license_key):
    """Get license information."""
    try:
        license_data = manager.licenses_ref.child(license_key).get()
        
        if not license_data:
            return jsonify({'error': 'License not found'}), 404
        
        return jsonify({
            'success': True,
            'license': license_data
        })
        
    except Exception as e:
        return jsonify({'error': str(e)}), 500


@app.route('/api/v1/licenses/<license_key>', methods=['DELETE'])
@require_auth
def revoke_license(license_key):
    """Revoke a license."""
    try:
        manager.revoke_license(license_key)
        
        return jsonify({
            'success': True,
            'message': 'License revoked'
        })
        
    except Exception as e:
        return jsonify({'error': str(e)}), 500


@app.route('/api/v1/licenses/<license_key>/extend', methods=['POST'])
@require_auth
def extend_license(license_key):
    """
    Extend license expiration.
    
    Request Body:
    {
        "days": 90
    }
    """
    try:
        data = request.get_json()
        days = data.get('days')
        
        if not days:
            return jsonify({'error': 'Missing days parameter'}), 400
        
        manager.extend_license(license_key, days)
        
        return jsonify({
            'success': True,
            'message': f'License extended by {days} days'
        })
        
    except Exception as e:
        return jsonify({'error': str(e)}), 500


if __name__ == '__main__':
    # For development only - use production server (gunicorn) for production
    app.run(host='0.0.0.0', port=5000, debug=False)
```

### 3. **Configure Environment**

Create `.env` file:
```env
LICENSE_API_SECRET=your-super-secret-key-change-this
VALID_API_KEYS=key-for-website,key-for-stripe,key-for-admin
```

### 4. **Run the API**

```bash
# Development
python license_api.py

# Production (with gunicorn)
pip install gunicorn
gunicorn -w 4 -b 0.0.0.0:5000 license_api:app
```

### 5. **Use the API**

```bash
# Create license
curl -X POST https://your-server.com/api/v1/licenses \
  -H "X-API-Key: your-api-key" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "customer@example.com",
    "app_id": "spec-updater",
    "plan": "premium",
    "duration_days": 365
  }'

# Get license info
curl -X GET https://your-server.com/api/v1/licenses/ABC123-DEF456 \
  -H "X-API-Key: your-api-key"

# Revoke license
curl -X DELETE https://your-server.com/api/v1/licenses/ABC123-DEF456 \
  -H "X-API-Key: your-api-key"
```

### Security for Flask API

✅ **Implemented:**
- API key authentication
- HTTPS required (configure reverse proxy)
- Input validation

⚠️ **Add for Production:**
```python
# Rate limiting
from flask_limiter import Limiter
from flask_limiter.util import get_remote_address

limiter = Limiter(
    app=app,
    key_func=get_remote_address,
    default_limits=["100 per hour"]
)

# IP whitelist
ALLOWED_IPS = ['192.168.1.1', '10.0.0.1']

@app.before_request
def check_ip():
    if request.remote_addr not in ALLOWED_IPS:
        abort(403)
```

---

## Option 2: Firebase Cloud Functions (Production)

**Best for:** Production environments, public-facing APIs, maximum security

### Why Cloud Functions?

✅ **Built-in Security:**
- Firebase Authentication
- Automatic HTTPS
- DDoS protection
- Serverless scaling

✅ **No Server Management:**
- Auto-scaling
- No infrastructure
- Built-in monitoring

### Setup

1. **Install Firebase CLI**
```bash
npm install -g firebase-tools
firebase login
```

2. **Initialize Functions**
```bash
cd Renamer
firebase init functions
# Choose Python as language
```

3. **Create Cloud Function** (`functions/main.py`)

```python
import firebase_admin
from firebase_admin import credentials, db, auth
from firebase_functions import https_fn
import secrets
import string
from datetime import datetime, timedelta

# Initialize Firebase Admin
cred = credentials.ApplicationDefault()
firebase_admin.initialize_app(cred, {
    'databaseURL': 'https://your-project.firebaseio.com'
})


def generate_license_key():
    """Generate a secure license key."""
    chars = string.ascii_uppercase + string.digits
    parts = []
    for _ in range(4):
        part = ''.join(secrets.choice(chars) for _ in range(5))
        parts.append(part)
    return '-'.join(parts)


@https_fn.on_call()
def createLicense(req: https_fn.CallableRequest):
    """
    Create a new license.
    Requires authentication.
    
    Args:
        email (str): Customer email
        app_id (str): Application ID
        plan (str): License plan
        duration_days (int): License duration
        max_devices (int): Maximum devices
        documents_limit (int): Document limit
    """
    # Check if user is authenticated
    if not req.auth:
        raise https_fn.HttpsError(
            code=https_fn.FunctionsErrorCode.UNAUTHENTICATED,
            message='User must be authenticated'
        )
    
    # Optional: Check if user has admin role
    # if not req.auth.token.get('admin'):
    #     raise https_fn.HttpsError(
    #         code=https_fn.FunctionsErrorCode.PERMISSION_DENIED,
    #         message='User must be admin'
    #     )
    
    # Validate input
    email = req.data.get('email')
    app_id = req.data.get('app_id')
    
    if not email or not app_id:
        raise https_fn.HttpsError(
            code=https_fn.FunctionsErrorCode.INVALID_ARGUMENT,
            message='email and app_id are required'
        )
    
    # Generate license
    license_key = generate_license_key()
    now = datetime.utcnow()
    duration_days = req.data.get('duration_days', 365)
    expires_at = now + timedelta(days=duration_days)
    
    license_data = {
        'license_key': license_key,
        'app_id': app_id,
        'email': email,
        'plan': req.data.get('plan', 'premium'),
        'status': 'active',
        'created_at': now.isoformat(),
        'expires_at': expires_at.isoformat(),
        'max_devices': req.data.get('max_devices', 3),
        'documents_limit': req.data.get('documents_limit', -1),
        'documents_used': 0,
        'created_by': req.auth.uid  # Track who created it
    }
    
    # Save to Firebase
    ref = db.reference('licenses')
    ref.child(license_key).set(license_data)
    
    return {
        'success': True,
        'license_key': license_key,
        'expires_at': expires_at.isoformat()
    }


@https_fn.on_call()
def getLicense(req: https_fn.CallableRequest):
    """Get license information."""
    if not req.auth:
        raise https_fn.HttpsError(
            code=https_fn.FunctionsErrorCode.UNAUTHENTICATED,
            message='User must be authenticated'
        )
    
    license_key = req.data.get('license_key')
    if not license_key:
        raise https_fn.HttpsError(
            code=https_fn.FunctionsErrorCode.INVALID_ARGUMENT,
            message='license_key is required'
        )
    
    ref = db.reference('licenses')
    license_data = ref.child(license_key).get()
    
    if not license_data:
        raise https_fn.HttpsError(
            code=https_fn.FunctionsErrorCode.NOT_FOUND,
            message='License not found'
        )
    
    return {'success': True, 'license': license_data}


@https_fn.on_call()
def revokeLicense(req: https_fn.CallableRequest):
    """Revoke a license."""
    if not req.auth:
        raise https_fn.HttpsError(
            code=https_fn.FunctionsErrorCode.UNAUTHENTICATED,
            message='User must be authenticated'
        )
    
    license_key = req.data.get('license_key')
    if not license_key:
        raise https_fn.HttpsError(
            code=https_fn.FunctionsErrorCode.INVALID_ARGUMENT,
            message='license_key is required'
        )
    
    ref = db.reference('licenses')
    ref.child(license_key).update({
        'status': 'suspended',
        'revoked_at': datetime.utcnow().isoformat(),
        'revoked_by': req.auth.uid
    })
    
    return {'success': True, 'message': 'License revoked'}
```

4. **Deploy Functions**
```bash
firebase deploy --only functions
```

### 5. **Call from JavaScript** (e.g., your website)

```javascript
import { getFunctions, httpsCallable } from 'firebase/functions';

const functions = getFunctions();
const createLicense = httpsCallable(functions, 'createLicense');

// User must be authenticated first
// auth.signInWithEmailAndPassword(...)

// Create license
const result = await createLicense({
  email: 'customer@example.com',
  app_id: 'spec-updater',
  plan: 'premium',
  duration_days: 365
});

console.log('License created:', result.data.license_key);
```

### 6. **Set Up Admin Users**

In Firebase Console → Authentication → Users:
1. Create admin user
2. Set custom claims:
```bash
firebase functions:shell
admin.auth().setCustomUserClaims('user-uid-here', { admin: true })
```

---

## Integration Examples

### Stripe Webhook Integration

```python
# Flask endpoint for Stripe webhooks
@app.route('/webhooks/stripe', methods=['POST'])
def stripe_webhook():
    """Handle successful payment from Stripe."""
    sig = request.headers.get('Stripe-Signature')
    
    # Verify webhook signature
    try:
        event = stripe.Webhook.construct_event(
            request.data, sig, os.getenv('STRIPE_WEBHOOK_SECRET')
        )
    except Exception as e:
        return jsonify({'error': str(e)}), 400
    
    # Handle successful payment
    if event['type'] == 'checkout.session.completed':
        session = event['data']['object']
        
        # Create license
        license_key = manager.create_license(
            email=session['customer_email'],
            app_id=session['metadata']['app_id'],
            plan=session['metadata']['plan'],
            duration_days=365
        )
        
        # Send license via email
        send_license_email(session['customer_email'], license_key)
        
        return jsonify({'success': True})
    
    return jsonify({'received': True})
```

### PayPal IPN Integration

```python
@app.route('/webhooks/paypal', methods=['POST'])
def paypal_ipn():
    """Handle PayPal Instant Payment Notification."""
    # Verify IPN with PayPal
    # ... verification code ...
    
    if request.form.get('payment_status') == 'Completed':
        license_key = manager.create_license(
            email=request.form.get('payer_email'),
            app_id=request.form.get('custom'),  # Pass app_id in custom field
            plan='premium',
            duration_days=365
        )
        
        # Email license to customer
        send_license_email(request.form.get('payer_email'), license_key)
    
    return '', 200
```

---

## Security Best Practices

### 1. Never Expose Admin SDK Directly

❌ **Don't:**
```python
# Exposing Firebase Admin SDK directly
@app.route('/create', methods=['POST'])
def create():
    # Anyone can call this!
    license_key = manager.create_license(...)
```

✅ **Do:**
```python
# Require authentication
@app.route('/create', methods=['POST'])
@require_auth
def create():
    # Only authenticated users
    license_key = manager.create_license(...)
```

### 2. Use Environment Variables

```bash
# .env file
LICENSE_API_SECRET=super-secret-key
STRIPE_WEBHOOK_SECRET=whsec_...
VALID_API_KEYS=key1,key2,key3
```

### 3. Enable Rate Limiting

```python
from flask_limiter import Limiter

limiter = Limiter(
    app=app,
    key_func=get_remote_address,
    default_limits=["100 per hour", "10 per minute"]
)

@app.route('/api/v1/licenses', methods=['POST'])
@limiter.limit("10 per hour")
@require_auth
def create_license():
    ...
```

### 4. Log All Operations

```python
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

@app.route('/api/v1/licenses', methods=['POST'])
@require_auth
def create_license():
    logger.info(f"License creation request from {request.remote_addr}")
    license_key = manager.create_license(...)
    logger.info(f"License created: {license_key}")
    return jsonify({'license_key': license_key})
```

### 5. Validate All Input

```python
from email_validator import validate_email, EmailNotValidError

@app.route('/api/v1/licenses', methods=['POST'])
@require_auth
def create_license():
    data = request.get_json()
    
    # Validate email
    try:
        validate_email(data['email'])
    except EmailNotValidError:
        return jsonify({'error': 'Invalid email'}), 400
    
    # Validate app_id
    valid_apps = ['spec-updater', 'coffee-stock-widget']
    if data['app_id'] not in valid_apps:
        return jsonify({'error': 'Invalid app_id'}), 400
    
    ...
```

---

## Summary

### Choose Your Approach:

| Scenario | Recommendation |
|----------|----------------|
| **Internal admin panel** | GUI Tool (`admin_gui.py`) |
| **Trusted webhooks** | Flask API with API keys |
| **Public-facing API** | Firebase Cloud Functions |
| **Enterprise scale** | Cloud Functions + OAuth 2.0 |

### Security Checklist:

- [ ] HTTPS only
- [ ] Authentication required
- [ ] Input validation
- [ ] Rate limiting
- [ ] Audit logging
- [ ] IP whitelisting (if internal)
- [ ] Environment variables for secrets
- [ ] Regular security audits

---

**Ready to implement?** Start with the GUI tool for immediate use, then add the API as needed!
