# Quick Reference: Firebase Integration

## TL;DR

✅ **Use ONE Firebase database** for all licensing
✅ **Renamer uses same database** - no separate database needed
✅ **Track usage** in `usage_logs` collection with `app_id` field
✅ **Bundle licenses** - create multiple entries or use bundle flag

---

## Database Flow

### When Customer Purchases License

1. **Payment processed** → Stripe/PayPal webhook
2. **Backend API** → Creates license in Firebase
3. **Email sent** → Customer receives license key
4. **Customer enters key** → In Renamer app
5. **App validates** → Checks Firebase database
6. **App tracks usage** → Logs to `usage_logs` collection

### Data Separation

**All apps use same collections:**
- `licenses/` - License keys (filtered by `app_id`)
- `device_activations/` - Device registrations (filtered by `app_id`)
- `usage_logs/` - Usage tracking (filtered by `app_id`)

**Query by app:**
```python
# Get licenses for spec-updater
licenses = db.child('licenses')\
    .order_by_child('app_id')\
    .equal_to('spec-updater')\
    .get()
```

---

## Usage Tracking

### How Renamer Tracks Usage

```python
# In your app code (main.py or worker)
subscription_mgr = SubscriptionManager(app_id="spec-updater")

# After processing documents
if subscription_mgr.check_document_limit():
    # Process files...
    files_processed = 10
    subscription_mgr.record_document_processed(count=files_processed)
```

This creates entry in Firebase:
```json
{
  "usage_logs": {
    "log_123": {
      "app_id": "spec-updater",
      "device_id": "...",
      "license_key": "...",
      "documents_processed": 10,
      "timestamp": "2024-01-15T10:30:00Z"
    }
  }
}
```

### Query Usage by App

```python
# Total documents for spec-updater
all_logs = db.reference('usage_logs').get()
spec_logs = {k: v for k, v in all_logs.items() if v['app_id'] == 'spec-updater'}
total = sum(log['documents_processed'] for log in spec_logs.values())
```

---

## Bundle License Handling

### Current Implementation

When bundle license is purchased, backend creates:

```json
{
  "licenses": {
    "BUNDLE-ABC-spec": {
      "license_key": "BUNDLE-ABC",
      "app_id": "spec-updater",
      "bundle_parent": "BUNDLE-ABC",
      "is_bundle": true
    },
    "BUNDLE-ABC-coffee": {
      "license_key": "BUNDLE-ABC",
      "app_id": "coffee-stock-widget",
      "bundle_parent": "BUNDLE-ABC",
      "is_bundle": true
    }
  }
}
```

### Validation Logic

Updated `subscription.py` to:
1. Try direct lookup: `licenses/{license_key}`
2. If not found, check bundle entries: `licenses/{license_key}-{app_id}`
3. Also check `bundle_parent` field
4. Accept if `is_bundle` is true and app matches

---

## Next Steps

1. ✅ **Backend API created** - `backend_api.py` example
2. ⚠️ **Deploy backend** - Host on server (Heroku, AWS, etc.)
3. ⚠️ **Update portal JS** - Connect to real API (not simulated)
4. ⚠️ **Set up email** - Send license keys via email
5. ⚠️ **Configure webhooks** - Stripe/PayPal → Backend API

---

## Files Created

- `FIREBASE_INTEGRATION.md` - Full integration guide
- `BACKEND_API_GUIDE.md` - API implementation guide
- `DATABASE_ARCHITECTURE.md` - Database structure explanation
- `backend_api.py` - Flask example backend

---

## Quick Test

```bash
# Test backend API (after setup)
curl -X POST http://localhost:5000/api/create-license \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "product": "spec-updater",
    "plan": "premium",
    "durationDays": 365
  }'
```

This creates a license in Firebase that Renamer can use!

