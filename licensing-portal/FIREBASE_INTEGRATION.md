# Firebase Database Integration Guide

## Overview

**You use ONE Firebase database for ALL licensing across ALL applications.**

- ✅ **Single Source of Truth** - One Firebase project manages all licenses
- ✅ **Multi-App Support** - `app_id` field differentiates between programs
- ✅ **Bundle Support** - Special handling for suite bundles
- ✅ **Usage Tracking** - All apps log usage to same database

---

## Database Structure

### Firebase Realtime Database Collections

```
Firebase Project: "licenses"
├── licenses/
│   └── [LICENSE_KEY]/
│       ├── license_key
│       ├── app_id              // "spec-updater", "bundle", etc.
│       ├── plan                // "basic", "premium", "business"
│       ├── email
│       ├── status              // "active", "expired", "suspended"
│       ├── created_at
│       ├── expires_at
│       ├── max_devices
│       ├── documents_limit
│       ├── documents_used
│       └── stripe_customer_id  // For payment tracking
│
├── device_activations/
│   └── [DEVICE_ID]/
│       ├── device_id
│       ├── app_id              // Which app this activation is for
│       ├── license_key
│       ├── device_name
│       ├── activated_at
│       └── last_validated
│
└── usage_logs/
    └── [LOG_ID]/
        ├── device_id
        ├── app_id              // Which app processed documents
        ├── license_key
        ├── documents_processed
        ├── timestamp
        └── app_version
```

---

## How Renamer Tracks Usage

### Current Flow

1. **App starts** → `SubscriptionManager(app_id="spec-updater")`
2. **User enters license** → Validates against `licenses/[KEY]`
3. **Device activated** → Creates entry in `device_activations/[DEVICE_ID]`
4. **Documents processed** → Logs to `usage_logs` with `app_id="spec-updater"`

### Usage Tracking Example

```python
# In Renamer app (main.py or worker thread)
subscription_mgr = SubscriptionManager(app_id="spec-updater")

# After processing documents
if subscription_mgr.check_document_limit():
    # Process documents...
    subscription_mgr.record_document_processed(count=10)
```

This creates an entry in Firebase:
```json
{
  "usage_logs": {
    "log_123": {
      "app_id": "spec-updater",
      "device_id": "device-uuid",
      "license_key": "ABC12-DEF34-GHI56-JKL78",
      "documents_processed": 10,
      "timestamp": "2024-01-15T10:30:00Z",
      "app_version": "1.0.0"
    }
  }
}
```

---

## Bundle License Handling

### Problem
Bundle licenses work for ALL apps, but the database uses `app_id` to filter.

### Solution: Multiple App IDs

**Option 1: Store as "bundle" and check all apps**
```json
{
  "license_key": "BUNDLE-XXXXX-XXXXX",
  "app_id": "bundle",              // Special identifier
  "plan": "premium",
  "bundle_apps": ["spec-updater", "coffee-stock-widget", "future-app"],
  // ... other fields
}
```

**Option 2: Create separate entries per app (Recommended)**
When a bundle license is created, create entries for each app:
```json
{
  "licenses": {
    "BUNDLE-XXXXX": {
      "license_key": "BUNDLE-XXXXX",
      "app_id": "spec-updater",
      "plan": "premium",
      "bundle_parent": "BUNDLE-XXXXX",
      // ... other fields
    },
    // Same license, different app_id
    "BUNDLE-XXXXX-spec": {
      // Same data, app_id: "spec-updater"
    }
  }
}
```

**Option 3: Use bundle flag (Simplest)**
```json
{
  "license_key": "BUNDLE-XXXXX",
  "app_id": "bundle",
  "plan": "premium",
  "is_bundle": true,
  "bundle_tier": "premium"
}
```

Then in validation, check if `app_id == "bundle"` OR `app_id == current_app`.

---

## Licensing Portal → Firebase Integration

### What the Portal Needs

The licensing portal needs a **backend API** that:

1. **Processes payment** (Stripe/PayPal webhook)
2. **Creates license in Firebase**
3. **Sends license key to customer**

### Backend API Endpoints

```javascript
// POST /api/process-payment
// 1. Verify payment with Stripe/PayPal
// 2. Create license in Firebase
// 3. Return license key

// POST /api/create-license
// Creates license in Firebase (called after payment)
```

---

## Backend API Example

### Node.js/Express Example

```javascript
const admin = require('firebase-admin');
const functions = require('firebase-functions');

// Initialize Firebase Admin
admin.initializeApp();

// Create license after payment
async function createLicense(data) {
  const {
    licenseKey,
    email,
    product,      // "spec-updater" or "bundle"
    plan,         // "basic", "premium", "business"
    expiresAt,
    paymentId
  } = data;

  const db = admin.database();
  
  // Determine app_id and plan details
  const planConfig = getPlanConfig(product, plan);
  
  // If bundle, create entries for all apps
  if (product === 'bundle') {
    const apps = ['spec-updater', 'coffee-stock-widget']; // Current apps
    
    for (const appId of apps) {
      await db.ref(`licenses/${licenseKey}-${appId}`).set({
        license_key: licenseKey,
        app_id: appId,
        bundle_parent: licenseKey,
        email: email,
        plan: plan,
        status: 'active',
        created_at: new Date().toISOString(),
        expires_at: expiresAt,
        max_devices: planConfig.max_devices,
        documents_limit: planConfig.documents_limit,
        documents_used: 0,
        stripe_customer_id: paymentId,
        is_bundle: true
      });
    }
  } else {
    // Single app license
    await db.ref(`licenses/${licenseKey}`).set({
      license_key: licenseKey,
      app_id: product,
      email: email,
      plan: plan,
      status: 'active',
      created_at: new Date().toISOString(),
      expires_at: expiresAt,
      max_devices: planConfig.max_devices,
      documents_limit: planConfig.documents_limit,
      documents_used: 0,
      stripe_customer_id: paymentId,
      is_bundle: false
    });
  }
  
  return licenseKey;
}
```

---

## Updated SubscriptionManager for Bundles

The Renamer app needs to check for bundle licenses:

```python
def validate_license_key(self, license_key: str) -> bool:
    # Try app-specific license first
    license_data = self.db.child('licenses').child(license_key).get()
    
    if not license_data:
        # Try bundle license
        # Check if license exists with app_id == "bundle"
        bundle_license = self.db.child('licenses').order_by_child('license_key').equal_to(license_key).get()
        # Filter for bundle licenses
        # ...
    
    # Continue validation...
```

---

## Recommended Database Schema Update

### Enhanced License Structure

```json
{
  "licenses": {
    "ABC12-DEF34-GHI56": {
      "license_key": "ABC12-DEF34-GHI56",
      "app_id": "spec-updater",        // OR "bundle" for bundles
      "email": "user@example.com",
      "plan": "premium",
      "tier": "premium",                // Same as plan (for clarity)
      "status": "active",
      "created_at": "2024-01-15T00:00:00Z",
      "expires_at": "2025-01-15T00:00:00Z",
      "max_devices": 5,
      "documents_limit": -1,            // -1 = unlimited
      "documents_used": 0,
      
      // Bundle-specific fields
      "is_bundle": false,
      "bundle_parent_key": null,        // If part of bundle, reference parent
      
      // Payment tracking
      "stripe_customer_id": "cus_xxx",
      "stripe_subscription_id": "sub_xxx",
      "payment_method": "stripe",       // or "paypal"
      
      // Metadata
      "purchased_product": "spec-updater",  // Original product purchased
      "purchased_tier": "premium"           // Original tier purchased
    }
  }
}
```

---

## Implementation Steps

### Step 1: Update Firebase Rules (If Needed)

Your current rules support this structure. No changes needed unless you want to add bundle-specific rules.

### Step 2: Create Backend API

The licensing portal needs a backend that:
1. Receives payment webhooks
2. Creates licenses in Firebase
3. Sends emails with license keys

### Step 3: Update SubscriptionManager (Optional)

Add bundle license checking to your Python app.

### Step 4: Update Admin Tools

Update `admin_license_manager.py` to handle bundle creation.

---

## Usage Tracking by App

### Query Usage by App

```python
# Get all usage for spec-updater
usage_ref = db.reference('usage_logs')
all_logs = usage_ref.order_by_child('app_id').equal_to('spec-updater').get()

# Get usage for specific license
license_usage = usage_ref.order_by_child('license_key').equal_to('ABC12-DEF34').get()
```

### Analytics Example

```python
# Total documents processed by app
def get_app_usage_stats(app_id: str):
    logs = db.reference('usage_logs').order_by_child('app_id').equal_to(app_id).get()
    total = sum(log['documents_processed'] for log in logs.values())
    return total
```

---

## Key Points

✅ **One Database** - Use single Firebase project for all apps
✅ **app_id Field** - Differentiates between programs
✅ **Usage Tracking** - All apps log to `usage_logs` with app_id
✅ **Bundle Support** - Can use special app_id or multiple entries
✅ **No Separate Database Needed** - Everything in one place

---

## Next Steps

1. **Create backend API** for licensing portal
2. **Update database schema** if needed for bundles
3. **Test license creation** from portal
4. **Verify Renamer can read licenses** created by portal
5. **Set up payment webhooks** (Stripe/PayPal)

