# Firebase Security Rules Guide

## 🔒 Security Rules Overview

Your current Firebase rules are **production-grade** with authentication requirements. The DesktopHub app needs additional rules for new features.

---

## 📋 Missing Rules in Your Current Setup

Your current rules are **missing** these paths that DesktopHub uses:

1. ❌ **`app_versions`** - Required for update checking
2. ❌ **`device_heartbeats`** - Required for active device tracking
3. ❌ **`error_logs`** - Required for error logging
4. ❌ **`update_checks`** - Required for tracking update check analytics

---

## ✅ Complete Rules

I've created **two rule sets** for you:

### **1. Development Rules** (`firebase-rules-development.json`)

**Use during development** - allows all writes for testing:

```json
{
  "rules": {
    "app_versions": {
      "$appId": {
        ".read": true,
        ".write": true  // ← Allows update script to work
      }
    },
    // ... other paths with permissive rules
  }
}
```

**Benefits:**
- ✅ Update script works without authentication
- ✅ Easy testing and debugging
- ✅ No auth token needed

**Drawbacks:**
- ⚠️ Anyone can write (only use in development Firebase project)

---

### **2. Production Rules** (`firebase-rules-production.json`)

**Use in production** - secure with authentication:

```json
{
  "rules": {
    "app_versions": {
      "$appId": {
        ".read": true,              // Anyone can read (check for updates)
        ".write": "auth != null"    // Only authenticated users can write
      }
    },
    // ... other paths with strict auth requirements
  }
}
```

**Security:**
- ✅ Requires service account authentication for writes
- ✅ Public read access for update checks
- ✅ Device-specific write access where needed

---

## 🚀 Quick Setup

### **For Development (Right Now)**

1. **Upload development rules:**
   - Go to: https://console.firebase.google.com/project/licenses-ff136/database/licenses-ff136-default-rtdb/rules
   - Copy contents of `firebase-rules-development.json`
   - Paste and click **Publish**

2. **Test update script:**
   ```powershell
   cd scripts
   .\update-version.ps1 "1.0.0" "Initial release"
   ```

3. **Test app:**
   - Run DesktopHub
   - Check for updates (should work!)
   - View logs and Firebase Console

---

### **For Production (Later)**

When deploying to real users:

1. **Switch to production rules:**
   - Copy `firebase-rules-production.json`
   - Paste in Firebase Console
   - Click **Publish**

2. **Update the update script** to use service account auth:
   - Use `Update-FirebaseVersion.ps1` (full version with auth)
   - Or manually authenticate with `gcloud`

---

## 🔑 Authentication Explained

### **Current Rules Use:**

```json
"auth != null && auth.uid == $deviceId"
```

**Problem:** DesktopHub uses **service account** authentication (via Firebase Admin SDK), not **user auth**.

**In service account auth:**
- `auth.uid` = service account email
- `auth` contains token info
- Device ID ≠ auth.uid

### **Solution:**

The production rules use `"auth != null"` for writes, which allows **any authenticated request** (including service accounts).

For stricter security, you could add:
```json
".write": "auth != null && auth.token.email.endsWith('@licenses-ff136.iam.gserviceaccount.com')"
```

This ensures only **your service account** can write.

---

## 📊 Rule Breakdown

### **What Each Rule Does:**

| Path | Read | Write | Purpose |
|------|------|-------|---------|
| `app_versions` | Public | Authenticated | Update checking |
| `device_activations` | Device only | Device only | Device registration |
| `device_heartbeats` | Device only | Device only | Active status |
| `app_launches` | None | Authenticated | Launch tracking |
| `error_logs` | None | Authenticated | Error logging |
| `licenses` | License owner | Auto-create free | License management |
| `update_checks` | None | Authenticated | Update analytics |

---

## 🛠️ Fixing Your Current Rules

### **Option 1: Quick Fix (Development)**

Add these 4 blocks to your existing rules:

```json
{
  "rules": {
    // ... your existing rules ...
    
    "device_heartbeats": {
      "$deviceId": {
        ".read": "auth != null && auth.uid == $deviceId",
        ".write": "auth != null && auth.uid == $deviceId",
        ".indexOn": ["app_id", "status", "last_seen"]
      }
    },
    "error_logs": {
      "$errorId": {
        ".read": false,
        ".write": "auth != null",
        ".indexOn": ["device_id", "timestamp", "app_id", "error_type"]
      }
    },
    "update_checks": {
      "$checkId": {
        ".read": false,
        ".write": "auth != null",
        ".indexOn": ["device_id", "timestamp", "app_id"]
      }
    },
    "app_versions": {
      "$appId": {
        ".read": true,
        ".write": "auth != null",
        ".indexOn": ["latest_version", "updated_at"]
      }
    }
  }
}
```

### **Option 2: Use Complete Rule Set**

Replace everything with `firebase-rules-production.json` for complete, secure rules.

---

## 🧪 Testing Rules

### **Test in Firebase Console:**

1. **Go to Rules Playground:**
   https://console.firebase.google.com/project/licenses-ff136/database/licenses-ff136-default-rtdb/rules

2. **Simulate operations:**
   ```
   Location: /app_versions/desktophub
   Type: Read
   Authenticated: No
   Result: Should allow ✅
   
   Location: /app_versions/desktophub
   Type: Write
   Authenticated: No
   Result: Should deny ❌
   
   Location: /app_versions/desktophub
   Type: Write
   Authenticated: Yes (Service account)
   Result: Should allow ✅
   ```

---

## 🔐 Security Best Practices

### **Development:**
- ✅ Use separate Firebase project for dev
- ✅ Permissive rules for easier testing
- ✅ Never expose dev credentials

### **Production:**
- ✅ Require authentication for all writes
- ✅ Validate data structure (`.hasChildren()`, `.isString()`)
- ✅ Add indexes for performance (`.indexOn`)
- ✅ Use specific auth checks where possible
- ✅ Never allow public writes
- ✅ Audit rules regularly

---

## ⚙️ Rule Updates Required

### **Immediate (for update script to work):**

Add to your current rules:

```json
"app_versions": {
  "$appId": {
    ".read": true,
    ".write": true  // Temporary for development
  }
}
```

### **Soon (for full app functionality):**

Add all missing paths from `firebase-rules-production.json`:
- `device_heartbeats`
- `error_logs`
- `update_checks`

### **Before Production:**

Replace development rules with production rules for security.

---

## 📝 Quick Commands

### **View current rules:**
```bash
# Via Firebase CLI
firebase database:get /
```

### **Deploy rules:**
```bash
# Via Firebase CLI
firebase deploy --only database
```

### **Export current rules:**
```bash
firebase database:get / > current-rules-backup.json
```

---

## 🎯 Recommended Action

**Right now:**

1. Backup your current rules (copy from Firebase Console)
2. Add the 4 missing paths (app_versions, device_heartbeats, error_logs, update_checks)
3. Use development-style rules (`.write: true`) for `app_versions` temporarily
4. Test the update script
5. Switch to production rules before deploying to users

This gives you a working setup immediately while keeping your existing security intact.
