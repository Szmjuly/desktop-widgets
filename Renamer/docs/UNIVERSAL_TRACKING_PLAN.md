# Universal License Tracking & Metrics System

## Overview

**All users get tracked** - whether free or paid. This allows:
- ✅ User identification and metrics
- ✅ Cost analysis and usage tracking
- ✅ Historical data collection
- ✅ Proper limit enforcement (server-side)
- ✅ Analytics for decision-making

---

## New Architecture

### License Types

1. **Free License** (Auto-assigned)
   - Automatically created on first run
   - Full features enabled (if configured)
   - Usage tracked for analytics
   - No expiration (or very long expiration)

2. **Paid Licenses** (Purchased)
   - Created via licensing portal
   - Based on tier (Basic, Premium, Business)
   - Enforces limits (devices, documents)
   - Expiration dates

### License Assignment Flow

```
User Opens App
    ↓
Check for existing license (local file)
    ↓
No license found?
    ↓
Auto-create Free License (or prompt for activation)
    ↓
Assign license in Firebase
    ↓
Track all usage
```

---

## Database Structure (Updated)

### Firebase Realtime Database (Recommended for Real-time)

**Realtime Database is good enough** for your use case. Firestore would be better for:
- Complex queries
- Large scale (millions of records)
- Better security rules

But Realtime DB works fine for < 100K licenses.

### Updated Collections

```
licenses/
├── [LICENSE_KEY]/
│   ├── license_key
│   ├── app_id
│   ├── plan                  // "free", "basic", "premium", "business"
│   ├── tier                  // Same as plan (for clarity)
│   ├── email                 // Optional for free licenses
│   ├── status                // "active", "expired", "suspended"
│   ├── source                // "auto-created", "purchased", "admin-created"
│   ├── created_at
│   ├── expires_at            // null for free (never expires)
│   ├── max_devices           // -1 = unlimited
│   ├── documents_limit       // -1 = unlimited, 0 = unlimited for free
│   ├── documents_used        // Tracked in Firebase
│   ├── last_active           // Last usage timestamp
│   ├── is_bundle
│   └── payment_info          // Only for paid licenses

device_activations/
└── [DEVICE_ID]/
    ├── device_id
    ├── app_id
    ├── license_key
    ├── device_name
    ├── activated_at
    └── last_validated

usage_logs/
└── [LOG_ID]/
    ├── device_id
    ├── app_id
    ├── license_key
    ├── documents_processed
    ├── timestamp
    └── app_version

user_metrics/           // NEW - Aggregated metrics
└── [LICENSE_KEY]/
    ├── total_documents_processed
    ├── total_sessions
    ├── first_used
    ├── last_used
    └── avg_documents_per_session
```

---

## Auto-License Assignment System

### Method 1: Silent Auto-Creation (Recommended)

User opens app → License auto-created in background → No prompt

### Method 2: Activation Popup

User opens app → Shows "Welcome! Activating your free license..." → Creates license

### Method 3: Email Registration (Optional)

User can optionally provide email for:
- License recovery
- Usage reports
- Upgrade notifications

---

## Server-Side Limit Enforcement

### Current Problem

Limits are checked client-side → Easy to bypass

### Solution: Server-Side Validation

1. **Before processing** → Check limit via Firebase
2. **During processing** → Periodically verify limit
3. **After processing** → Update usage count atomically

### Atomic Operations

Use Firebase transactions to prevent race conditions:

```python
def check_and_reserve_documents(license_key, count):
    """Atomically check limit and reserve documents."""
    # This needs to be done server-side (Cloud Function)
    # Or use Firebase transactions
```

---

## Implementation Plan

### Step 1: Update SubscriptionManager

Add methods for:
- Auto-creating free licenses
- Always tracking (even if free)
- Server-side limit checking

### Step 2: Update Main App

- Auto-assign license on startup (if none exists)
- Check limits before/during processing
- Log all usage

### Step 3: Update Database Rules

- Allow auto-license creation
- Enforce limits server-side
- Track metrics

### Step 4: Cloud Functions (Optional but Recommended)

For true server-side enforcement:
- Firebase Cloud Functions
- Check limits before processing
- Atomic updates

---

## Database Choice: Realtime vs Firestore

### Recommendation: **Realtime Database** (For Now)

**Why Realtime DB:**
- ✅ Already implemented
- ✅ Simpler for your scale
- ✅ Real-time updates work great
- ✅ Lower cost for < 100K records

**Switch to Firestore if:**
- You exceed 100K licenses
- Need complex queries
- Want better security rules
- Need offline sync features

**Hybrid Approach:**
- Realtime DB: licenses, activations (frequent reads)
- Firestore: usage_logs, analytics (better for queries)

---

## Next Steps

1. Update subscription.py for auto-license creation
2. Update database rules
3. Add server-side limit checking
4. Update worker thread to enforce limits
5. Create metrics collection system

