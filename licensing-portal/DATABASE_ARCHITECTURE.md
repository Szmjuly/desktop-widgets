# Database Architecture - Single vs Multiple Databases

## Answer: Use ONE Firebase Database

**You should use ONE Firebase database for all licensing.**

### Why One Database?

✅ **Single Source of Truth** - All license data in one place
✅ **Easier Management** - One admin panel, one set of rules
✅ **Cross-App Analytics** - See usage across all apps
✅ **Bundle Support** - Easier to handle multi-app licenses
✅ **Simpler Maintenance** - One database to backup/update
✅ **Cost Effective** - Single Firebase project

---

## Database Structure Overview

### Single Firebase Project Structure

```
Firebase Project: "licenses"
│
├── licenses/
│   ├── ABC12-DEF34-GHI56/          # Spec Manager license
│   │   └── app_id: "spec-updater"
│   │
│   ├── BUNDLE-XXXXX-spec/          # Bundle license (spec-updater)
│   │   └── app_id: "spec-updater"
│   │   └── bundle_parent: "BUNDLE-XXXXX"
│   │
│   └── BUNDLE-XXXXX-coffee/        # Bundle license (coffee-widget)
│       └── app_id: "coffee-stock-widget"
│       └── bundle_parent: "BUNDLE-XXXXX"
│
├── device_activations/
│   ├── device-123/
│   │   └── app_id: "spec-updater"
│   │   └── license_key: "ABC12-DEF34-GHI56"
│   │
│   └── device-456/
│       └── app_id: "spec-updater"
│       └── license_key: "BUNDLE-XXXXX"
│
└── usage_logs/
    ├── log-001/
    │   └── app_id: "spec-updater"
    │   └── documents_processed: 10
    │
    └── log-002/
        └── app_id: "coffee-stock-widget"
        └── documents_processed: 5
```

---

## How Renamer Uses the Database

### Current Implementation

The Renamer app (`SubscriptionManager`) does:

1. **Initializes with app_id:**
   ```python
   sub_mgr = SubscriptionManager(app_id="spec-updater")
   ```

2. **Validates license:**
   ```python
   # Queries: licenses/{license_key}
   # Checks: license['app_id'] == "spec-updater"
   license = db.child('licenses').child(license_key).get()
   ```

3. **Registers device:**
   ```python
   # Creates: device_activations/{device_id}
   # Sets: app_id, license_key, device_name
   db.child('device_activations').child(device_id).set({
       'app_id': 'spec-updater',
       'license_key': license_key,
       ...
   })
   ```

4. **Logs usage:**
   ```python
   # Creates: usage_logs/{log_id}
   # Sets: app_id, documents_processed, timestamp
   db.child('usage_logs').push({
       'app_id': 'spec-updater',
       'documents_processed': 10,
       ...
   })
   ```

---

## Usage Tracking: Per-App Data

### How to Query Usage by App

```python
# Get all usage for spec-updater
usage_ref = db.reference('usage_logs')
spec_usage = usage_ref.order_by_child('app_id').equal_to('spec-updater').get()

# Count total documents processed
total_docs = sum(log['documents_processed'] for log in spec_usage.values())
```

### Analytics Queries

```python
# Documents processed per app
def get_usage_by_app():
    all_logs = db.reference('usage_logs').get()
    
    by_app = {}
    for log in all_logs.values():
        app = log['app_id']
        by_app[app] = by_app.get(app, 0) + log['documents_processed']
    
    return by_app

# Result:
# {
#   "spec-updater": 1500,
#   "coffee-stock-widget": 200
# }
```

---

## Bundle License Handling

### Option A: Multiple Entries (Recommended)

When bundle is purchased, create entry for each app:

```json
{
  "licenses": {
    "BUNDLE-ABC-spec": {
      "license_key": "BUNDLE-ABC",
      "app_id": "spec-updater",
      "bundle_parent": "BUNDLE-ABC",
      "is_bundle": true,
      "plan": "premium"
    },
    "BUNDLE-ABC-coffee": {
      "license_key": "BUNDLE-ABC",
      "app_id": "coffee-stock-widget",
      "bundle_parent": "BUNDLE-ABC",
      "is_bundle": true,
      "plan": "premium"
    }
  }
}
```

**Validation in Renamer:**
```python
# Try app-specific first
license = db.child('licenses').child(f"{license_key}-{app_id}").get()

# If not found, check for bundle
if not license:
    bundle_query = db.child('licenses')\
        .order_by_child('bundle_parent')\
        .equal_to(license_key)\
        .get()
    
    for key, data in bundle_query.items():
        if data['app_id'] == self.app_id:
            license = data
            break
```

### Option B: Single Entry with Bundle Flag

```json
{
  "licenses": {
    "BUNDLE-ABC": {
      "license_key": "BUNDLE-ABC",
      "app_id": "bundle",
      "is_bundle": true,
      "bundle_apps": ["spec-updater", "coffee-stock-widget"],
      "plan": "premium"
    }
  }
}
```

**Validation in Renamer:**
```python
license = db.child('licenses').child(license_key).get()

if license:
    if license['app_id'] == self.app_id:
        # Direct app license
        valid = True
    elif license.get('is_bundle') and self.app_id in license.get('bundle_apps', []):
        # Bundle license
        valid = True
```

---

## Do You Need Separate Databases?

### ❌ **No, You Don't Need Separate Databases**

**Reasons:**
1. **app_id field** already separates data
2. **Security rules** can filter by app_id
3. **Queries** filter by app_id
4. **Bundle licenses** easier with shared database
5. **Analytics** easier across apps
6. **Maintenance** simpler with one DB

### When You MIGHT Need Separate Databases

Only if:
- Different security requirements per app
- Regulatory compliance (data must be physically separated)
- Extreme scale (millions of records per app)

**For your use case:** One database is perfect.

---

## Data Separation Methods

### Method 1: app_id Field (Current - Recommended)

```python
# All apps query same collection
licenses = db.child('licenses')\
    .order_by_child('app_id')\
    .equal_to('spec-updater')\
    .get()
```

✅ **Pros:**
- Simple
- Cross-app queries possible
- Bundle support easy

### Method 2: Separate Collections

```
licenses_spec_updater/
licenses_coffee_widget/
```

❌ **Not Recommended** - More complex, harder to manage bundles

### Method 3: Separate Projects

```
firebase-project-spec/
firebase-project-coffee/
```

❌ **Not Recommended** - Unnecessary complexity

---

## Summary

| Question | Answer |
|----------|--------|
| **Separate database for Renamer?** | ❌ No - Use one database |
| **How to separate data?** | ✅ Use `app_id` field |
| **How to track usage?** | ✅ Log to `usage_logs` with `app_id` |
| **Bundle support?** | ✅ Multiple entries or bundle flag |
| **Query usage by app?** | ✅ Filter `usage_logs` by `app_id` |

**Your current setup is correct!** Just need to add backend API to create licenses from the portal.

