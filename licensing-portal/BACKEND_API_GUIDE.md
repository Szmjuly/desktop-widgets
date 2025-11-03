# Backend API - License Creation

## Overview

This document describes the backend API needed to connect the licensing portal to Firebase.

**Architecture:**
```
Licensing Portal (Frontend)
    ↓ [Payment via Stripe/PayPal]
Payment Provider Webhook
    ↓ [Payment confirmed]
Backend API Server
    ↓ [Creates license]
Firebase Database
    ↓ [Sends email]
Customer receives license key
```

---

## Required Backend Endpoints

### 1. Process Payment & Create License

**Endpoint:** `POST /api/process-payment`

**Request:**
```json
{
  "email": "customer@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "product": "spec-updater",      // or "bundle"
  "plan": "premium",               // "basic", "premium", "business"
  "paymentMethod": "stripe",       // or "paypal"
  "paymentToken": "tok_xxx",       // Stripe token
  "amount": 1999                   // in cents ($19.99)
}
```

**Response:**
```json
{
  "success": true,
  "licenseKey": "ABC12-DEF34-GHI56-JKL78",
  "expiresAt": "2025-01-15T00:00:00Z",
  "transactionId": "txn_xxx"
}
```

### 2. Create License (Direct - for admin/testing)

**Endpoint:** `POST /api/create-license`

**Request:**
```json
{
  "email": "customer@example.com",
  "product": "spec-updater",
  "plan": "premium",
  "durationDays": 365,
  "licenseKey": "ABC12-DEF34-GHI56-JKL78"  // Optional, auto-generated if not provided
}
```

---

## Backend Implementation (Python Example)

### Flask/FastAPI Example

```python
from flask import Flask, request, jsonify
import firebase_admin
from firebase_admin import credentials, db
import secrets
import string
from datetime import datetime, timedelta
import stripe  # or PayPal SDK

app = Flask(__name__)

# Initialize Firebase
cred = credentials.Certificate('firebase-admin-key.json')
firebase_admin.initialize_app(cred, {
    'databaseURL': 'https://your-project.firebaseio.com'
})

# Plan configuration
PLAN_CONFIG = {
    'spec-updater': {
        'basic': {
            'max_devices': 2,
            'documents_limit': 100,
            'price': 999  # cents
        },
        'premium': {
            'max_devices': 5,
            'documents_limit': -1,  # unlimited
            'price': 1999
        },
        'business': {
            'max_devices': -1,  # unlimited
            'documents_limit': -1,
            'price': 4999
        }
    },
    'bundle': {
        'basic': {
            'max_devices': 3,
            'documents_limit': 100,
            'price': 2499
        },
        'premium': {
            'max_devices': 5,
            'documents_limit': -1,
            'price': 3999
        },
        'business': {
            'max_devices': -1,
            'documents_limit': -1,
            'price': 7999
        }
    }
}

def generate_license_key():
    """Generate license key: XXXXX-XXXXX-XXXXX-XXXXX"""
    chars = string.ascii_uppercase + string.digits
    parts = []
    for _ in range(4):
        part = ''.join(secrets.choice(chars) for _ in range(5))
        parts.append(part)
    return '-'.join(parts)

@app.route('/api/process-payment', methods=['POST'])
def process_payment():
    data = request.json
    email = data['email']
    product = data['product']
    plan = data['plan']
    payment_method = data['paymentMethod']
    payment_token = data['paymentToken']
    amount = data['amount']
    
    # 1. Process payment
    if payment_method == 'stripe':
        stripe.api_key = 'sk_live_xxx'  # Your secret key
        charge = stripe.Charge.create(
            amount=amount,
            currency='usd',
            source=payment_token,
            description=f'{product} - {plan} license'
        )
        payment_id = charge.id
    elif payment_method == 'paypal':
        # Process PayPal payment
        payment_id = process_paypal_payment(payment_token, amount)
    
    # 2. Generate license key
    license_key = generate_license_key()
    
    # 3. Calculate expiration
    duration_days = 365  # Annual subscription
    expires_at = (datetime.utcnow() + timedelta(days=duration_days)).isoformat()
    
    # 4. Get plan config
    config = PLAN_CONFIG[product][plan]
    
    # 5. Create license(s) in Firebase
    db_ref = db.reference()
    
    if product == 'bundle':
        # Create licenses for all apps
        apps = ['spec-updater', 'coffee-stock-widget']  # Add more as needed
        
        for app_id in apps:
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
            db_ref.child('licenses').child(f'{license_key}-{app_id}').set(license_data)
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
        db_ref.child('licenses').child(license_key).set(license_data)
    
    # 6. Send email with license key
    send_license_email(email, license_key, product, plan, expires_at)
    
    return jsonify({
        'success': True,
        'licenseKey': license_key,
        'expiresAt': expires_at,
        'transactionId': payment_id
    })

@app.route('/api/create-license', methods=['POST'])
def create_license():
    """Admin endpoint to create license without payment"""
    data = request.json
    
    license_key = data.get('licenseKey') or generate_license_key()
    email = data['email']
    product = data['product']
    plan = data['plan']
    duration_days = data.get('durationDays', 365)
    
    expires_at = (datetime.utcnow() + timedelta(days=duration_days)).isoformat()
    config = PLAN_CONFIG[product][plan]
    
    db_ref = db.reference()
    
    if product == 'bundle':
        apps = ['spec-updater', 'coffee-stock-widget']
        for app_id in apps:
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
            db_ref.child('licenses').child(f'{license_key}-{app_id}').set(license_data)
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
        db_ref.child('licenses').child(license_key).set(license_data)
    
    return jsonify({
        'success': True,
        'licenseKey': license_key,
        'expiresAt': expires_at
    })

def send_license_email(email, license_key, product, plan, expires_at):
    """Send license key email to customer"""
    # Implement email sending (SendGrid, AWS SES, etc.)
    pass

if __name__ == '__main__':
    app.run(debug=True)
```

---

## Integration with Licensing Portal

### Update payment.js

Replace `simulatePayment()` function:

```javascript
// In payment.js, replace simulatePayment()
async function simulatePayment(formData, paymentMethod) {
    const response = await fetch('/api/process-payment', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            email: formData.email,
            firstName: formData.firstName,
            lastName: formData.lastName,
            product: formData.productId,
            plan: formData.planId,
            paymentMethod: paymentMethod,
            paymentToken: formData.stripeToken, // Get from Stripe Elements
            amount: getPlanPrice(formData.productId, formData.planId) * 100 // Convert to cents
        })
    });
    
    if (!response.ok) {
        throw new Error('Payment failed');
    }
    
    return await response.json();
}
```

---

## Firebase Security Rules Update

Add rules for bundle licenses:

```json
{
  "rules": {
    "licenses": {
      "$licenseKey": {
        ".read": "auth != null && (
          $licenseKey == data.child('license_key').val() ||
          data.child('bundle_parent').val() == $licenseKey
        )",
        ".write": false
      }
    }
  }
}
```

---

## Usage Tracking

### How Renamer Uses the Database

1. **License Validation:**
   ```python
   # Checks licenses/{license_key} where app_id matches
   license = db.child('licenses').child(license_key).get()
   if license['app_id'] == self.app_id or license.get('is_bundle'):
       # Valid license
   ```

2. **Usage Logging:**
   ```python
   # Logs to usage_logs/{log_id}
   db.child('usage_logs').push({
       'app_id': 'spec-updater',
       'license_key': license_key,
       'documents_processed': 10,
       'timestamp': datetime.utcnow().isoformat()
   })
   ```

3. **Document Limit Check:**
   ```python
   # Checks documents_used vs documents_limit
   if license['documents_limit'] > 0:
       if license['documents_used'] >= license['documents_limit']:
           return False
   ```

---

## Summary

✅ **One Database** - Single Firebase project for all apps
✅ **app_id Separation** - Differentiates programs
✅ **Bundle Support** - Multiple entries or special app_id
✅ **Usage Tracking** - All apps log to same collection
✅ **Backend API Needed** - Portal requires server-side license creation

