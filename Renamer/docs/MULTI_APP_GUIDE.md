# Multi-Application License Management Guide

## Overview

This Firebase project is designed to manage licenses for **multiple applications** from a single centralized location. Each application has its own `app_id` that separates its license data while sharing the same Firebase infrastructure.

## Architecture Benefits

✅ **Single Firebase Project** - One project manages all your apps  
✅ **Centralized Management** - One admin tool for all licenses  
✅ **Cost Efficient** - Share Firebase free tier across apps  
✅ **Easier Maintenance** - Update security rules once  
✅ **Unified Billing** - One invoice for all apps  
✅ **Scalable** - Add new apps without new Firebase projects  

---

## Supported Applications

### Current Applications

| App ID | Application Name | Description |
|--------|-----------------|-------------|
| `spec-updater` | Spec Header Date Updater | Updates dates in Word document headers |
| `coffee-stock-widget` | Coffee Stock Widget | Desktop widget for coffee inventory |

### Adding New Applications

When you create a new application:

1. **Choose an app_id** - Use kebab-case (e.g., `my-new-app`)
2. **Update your app code** - Initialize SubscriptionManager with your app_id
3. **Create licenses** - Use the admin tool with `--app-id`
4. **That's it!** - No new Firebase project needed

---

## Creating Licenses for Different Apps

### Spec Header Date Updater

```bash
python admin_license_manager.py create \
  --email customer@example.com \
  --app-id spec-updater \
  --plan premium \
  --duration 365
```

### Coffee Stock Widget

```bash
python admin_license_manager.py create \
  --email customer@example.com \
  --app-id coffee-stock-widget \
  --plan premium \
  --duration 365
```

### Any Future App

```bash
python admin_license_manager.py create \
  --email customer@example.com \
  --app-id your-new-app \
  --plan premium \
  --duration 365
```

---

## Managing Licenses

### List All Licenses

```bash
# All licenses across all apps
python admin_license_manager.py list

# Only Spec Updater licenses
python admin_license_manager.py list --app-id spec-updater

# Only Coffee Widget licenses
python admin_license_manager.py list --app-id coffee-stock-widget

# Filter by email and app
python admin_license_manager.py list --email user@example.com --app-id spec-updater
```

### View License Details

```bash
python admin_license_manager.py info --license ABCDE-12345-FGHIJ-67890
```

This shows which app the license is for, along with all activation details.

---

## Cross-App Licenses (Optional)

If you want to offer **bundle licenses** that work across multiple apps:

### Option 1: Separate Licenses (Current)
- Issue one license per app
- Customer enters different keys in each app
- Track separately in Firebase

### Option 2: Unified License (Requires Modification)
- Modify validation logic to accept `app_id: "all"`
- One license works in all apps
- Requires code changes in subscription.py

**Current Implementation:** Option 1 (Separate licenses per app)

---

## Integration in Your Apps

### Spec Header Date Updater

```python
from subscription import SubscriptionManager

# Initialize with app_id
subscription_mgr = SubscriptionManager(app_id="spec-updater")
```

### Coffee Stock Widget

```python
from subscription import SubscriptionManager

# Initialize with app_id
subscription_mgr = SubscriptionManager(app_id="coffee-stock-widget")
```

### New Application

```python
from subscription import SubscriptionManager

# Initialize with your app_id
subscription_mgr = SubscriptionManager(app_id="your-new-app")
```

---

## Firebase Data Structure

### Single Project: `licenses`

```
licenses/
├── licenses/
│   ├── ABC-123-DEF-456/
│   │   ├── app_id: "spec-updater"
│   │   ├── email: "user@example.com"
│   │   └── ...
│   └── GHI-789-JKL-012/
│       ├── app_id: "coffee-stock-widget"
│       ├── email: "user@example.com"
│       └── ...
├── device_activations/
│   └── {device-uuid}/
│       ├── app_id: "spec-updater"
│       └── ...
└── usage_logs/
    └── {log-id}/
        ├── app_id: "spec-updater"
        └── ...
```

---

## Security Considerations

### Data Isolation

✅ **App ID Validation** - Licenses only work for their designated app  
✅ **Separate Local Cache** - Each app stores its own subscription file  
✅ **Query Filtering** - Apps can only see their own data  

### Firebase Rules

The security rules enforce app-specific access:

```javascript
{
  ".indexOn": ["email", "status", "app_id"]
}
```

This allows efficient filtering by app_id.

---

## Migration from Separate Projects

If you previously had separate Firebase projects for each app:

### Step 1: Export Data

Export license data from old projects using Firebase Console.

### Step 2: Update app_id

Add `app_id` field to all exported licenses:

```json
{
  "license_key": "ABC-123",
  "app_id": "spec-updater",  // ADD THIS
  "email": "user@example.com",
  ...
}
```

### Step 3: Import to New Project

Import data into the new unified `licenses` project.

### Step 4: Update App Configuration

Point all apps to use the new `firebase_config.json` from the unified project.

### Step 5: Test

Validate that licenses still work in all apps.

---

## Cost Comparison

### Before (Separate Projects)

- Project 1: Free tier
- Project 2: Free tier
- **Total**: 2 projects to manage

### After (Single Project)

- Single Project: Free tier
- **Total**: 1 project to manage
- **Benefit**: Consolidated quota, easier management

---

## Best Practices

### 1. Consistent app_id Format

Use kebab-case for all app IDs:
- ✅ `spec-updater`
- ✅ `coffee-stock-widget`
- ❌ `SpecUpdater`
- ❌ `coffee_widget`

### 2. Document Your App IDs

Keep a list of all app_ids in use (like the table above).

### 3. Test New Apps

Before launching a new app:
1. Create test license with its app_id
2. Validate in the app
3. Check Firebase for correct data structure

### 4. Monitor Usage

Use Firebase Console to monitor usage across all apps:
- Database reads/writes
- Active users
- Storage usage

---

## Troubleshooting

### License Shows Wrong App

**Problem:** License created for wrong app_id

**Solution:**
```bash
# Check license details
python admin_license_manager.py info --license KEY

# If wrong app_id, revoke and recreate
python admin_license_manager.py revoke --license KEY
python admin_license_manager.py create --email user@example.com --app-id correct-app-id --plan premium --duration 365
```

### Can't See Licenses for Specific App

**Problem:** List command doesn't show app licenses

**Solution:**
```bash
# List with app filter
python admin_license_manager.py list --app-id your-app-id

# Check that licenses have app_id field
python admin_license_manager.py list
```

### License Works in Wrong App

**Problem:** License validating in incorrect application

**Solution:**
- Check that `subscription.py` validates `app_id` field
- Ensure security rules enforce app_id checking
- Verify app initialization uses correct app_id

---

## Example: Managing 3 Apps

```bash
# Create licenses for all three apps for same customer
python admin_license_manager.py create --email customer@example.com --app-id spec-updater --plan premium --duration 365
python admin_license_manager.py create --email customer@example.com --app-id coffee-stock-widget --plan basic --duration 365
python admin_license_manager.py create --email customer@example.com --app-id new-app --plan business --duration 365

# List all licenses for this customer
python admin_license_manager.py list --email customer@example.com

# Output shows:
# Found 3 license(s):
#
# License: ABC-123-DEF-456
#   App ID: spec-updater
#   Email: customer@example.com
#   ...
#
# License: GHI-789-JKL-012
#   App ID: coffee-stock-widget
#   Email: customer@example.com
#   ...
#
# License: MNO-345-PQR-678
#   App ID: new-app
#   Email: customer@example.com
#   ...
```

---

## Summary

✨ **One Firebase Project** manages all your application licenses  
🔑 **Unique app_id** identifies each application  
🛠️ **Single Admin Tool** handles all license operations  
📊 **Unified Analytics** across all your apps  
💰 **Cost Efficient** with shared infrastructure  

**Ready to add a new app?** Just initialize SubscriptionManager with a new app_id!
