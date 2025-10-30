# Firebase Subscription System Setup Guide

This guide provides complete step-by-step instructions for setting up a secure Firebase-based subscription system for the Spec Header Date Updater.

## Table of Contents
1. [Firebase Project Setup](#firebase-project-setup)
2. [Firebase Configuration](#firebase-configuration)
3. [Security Rules](#security-rules)
4. [Database Structure](#database-structure)
5. [Python Application Setup](#python-application-setup)
6. [Admin Tools](#admin-tools)
7. [Security Considerations](#security-considerations)

---

## 1. Firebase Project Setup

### Step 1: Create a Firebase Project

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Click "Add project" or "Create a project"
3. Enter project name: **`licenses`** (This single project will manage all your applications)
4. **Disable** Google Analytics (optional, but recommended for privacy)
5. Click "Create project"

**Note:** This single Firebase project will handle licenses for ALL your applications (Spec Updater, Coffee Stock Widget, etc.). Each app will have its own `app_id` field to separate data.

### Step 2: Enable Realtime Database (Not Firestore)

1. In your Firebase project console, click "Realtime Database" in the left menu
2. Click "Create Database"
3. **Select "Start in production mode"** (we'll add custom security rules)
4. Choose your database location (select closest to your users):
   - `us-central1` (Iowa) for US-based users
   - `europe-west1` (Belgium) for EU users
   - Other regions as needed
5. Click "Enable"

### Step 3: Set Up Authentication (Optional but Recommended)

1. Click "Authentication" in the left menu
2. Click "Get started"
3. Enable "Anonymous" authentication method
   - This allows device-specific authentication
   - Click "Anonymous" → Toggle "Enable" → Save

### Step 4: Create Service Account for Admin Operations

1. Click the gear icon (⚙️) next to "Project Overview"
2. Select "Project settings"
3. Go to the "Service accounts" tab
4. Click "Generate new private key"
5. Click "Generate key" in the dialog
6. **SAVE THIS FILE SECURELY** - it contains credentials to access your Firebase project
7. Rename the downloaded file to `firebase-admin-key.json`
8. **NEVER commit this file to version control**

### Step 5: Get Web API Key

1. In "Project settings" → "General" tab
2. Scroll down to "Your apps" section
3. Click the Web icon (</>) to add a web app
4. Register app with a nickname (e.g., "Spec Updater Client")
5. **Copy the `apiKey` from the config object** - you'll need this
6. Click "Continue to console"

---

## 2. Firebase Configuration

### Create Firebase Config File

Create a file named `firebase_config.json` in the Renamer directory:

```json
{
  "apiKey": "YOUR_API_KEY_HERE",
  "authDomain": "your-project-id.firebaseapp.com",
  "projectId": "your-project-id",
  "storageBucket": "your-project-id.appspot.com",
  "databaseURL": "https://your-project-id.firebaseio.com"
}
```

**Replace the values:**
- `YOUR_API_KEY_HERE`: The API key from Step 5 above
- `your-project-id`: Your actual Firebase project ID

**IMPORTANT:** Add this file to `.gitignore`:
```
firebase_config.json
firebase-admin-key.json
```

---

## 3. Security Rules

### Realtime Database Security Rules

**Important:** We're using **Realtime Database** (not Firestore). The rules syntax is different!

#### Method 1: Copy from firebase-database-rules.json

1. Open the file `firebase-database-rules.json` in your Renamer folder
2. Copy the entire contents
3. In Firebase Console → Realtime Database → Rules tab
4. Paste the rules
5. Click "Publish"

#### Method 2: Manual Entry

In Firebase Console → **Realtime Database** → **Rules** tab, paste these rules:

```json
{
  "rules": {
    "licenses": {
      "$licenseKey": {
        ".read": "auth != null && $licenseKey == data.child('license_key').val()",
        ".write": false,
        ".indexOn": ["email", "status", "app_id"]
      }
    },
    "device_activations": {
      "$deviceId": {
        ".read": "auth != null && auth.uid == $deviceId",
        ".write": "auth != null && auth.uid == $deviceId && newData.hasChildren(['app_id', 'license_key', 'activated_at', 'last_validated']) && newData.child('app_id').isString() && newData.child('license_key').isString() && newData.child('activated_at').isString() && newData.child('last_validated').isString()",
        ".indexOn": ["license_key", "app_id"]
      }
    },
    "usage_logs": {
      "$logId": {
        ".read": false,
        ".write": "auth != null && newData.hasChildren(['app_id', 'device_id', 'license_key', 'documents_processed', 'timestamp']) && newData.child('app_id').isString() && newData.child('device_id').isString() && newData.child('license_key').isString() && newData.child('documents_processed').isNumber() && newData.child('timestamp').isString()",
        ".indexOn": ["device_id", "license_key", "timestamp", "app_id"]
      }
    }
  }
}
```

**Click "Publish" to save the rules.**

**Note:** Make sure you're in **Realtime Database** → **Rules**, NOT "Firestore Database" → "Rules"!

---

## 4. Database Structure

### Collections Schema

#### `licenses` Collection

Each document represents a license key:

```javascript
{
  "license_key": "ABC123-DEF456-GHI789",  // Document ID
  "app_id": "spec-updater",                // Application identifier (NEW!)
  "email": "customer@example.com",
  "plan": "premium",                       // "free", "basic", "premium"
  "status": "active",                      // "active", "expired", "suspended"
  "created_at": Timestamp,
  "expires_at": Timestamp,
  "max_devices": 3,                        // Maximum number of devices
  "documents_limit": -1,                   // -1 for unlimited
  "documents_used": 0,
  "stripe_customer_id": "cus_xxxxx",       // Optional: for payment integration
  "stripe_subscription_id": "sub_xxxxx"    // Optional
}
```

**Supported `app_id` values:**
- `spec-updater` - Spec Header Date Updater
- `coffee-stock-widget` - Coffee Stock Widget
- (Add more as you create new applications)
```

#### `device_activations` Collection

Tracks which devices are using which licenses:

```javascript
{
  "device_id": "uuid-of-device",           // Document ID
  "app_id": "spec-updater",                // Application identifier (NEW!)
  "license_key": "ABC123-DEF456-GHI789",
  "device_name": "DESKTOP-ABC123",
  "activated_at": Timestamp,
  "last_validated": Timestamp,
  "app_version": "1.0.0"
}
```

#### `usage_logs` Collection

Tracks usage for analytics and billing:

```javascript
{
  "device_id": "uuid-of-device",
  "app_id": "spec-updater",                // Application identifier (NEW!)
  "license_key": "ABC123-DEF456-GHI789",
  "documents_processed": 5,
  "timestamp": Timestamp,
  "app_version": "1.0.0"
}
```

---

## 5. Python Application Setup

### Install Required Dependencies

```bash
pip install firebase-admin pyrebase4 python-dotenv
```

Or update your `requirements.txt` (we'll create this).

### Environment Variables

Create a `.env` file in the Renamer directory:

```env
# Firebase Configuration
FIREBASE_ADMIN_KEY_PATH=firebase-admin-key.json
FIREBASE_CONFIG_PATH=firebase_config.json

# Application Settings
APP_VERSION=1.0.0
```

Add `.env` to `.gitignore`.

---

## 6. Admin Tools

### Option A: GUI Tool (Recommended)

Launch the graphical admin interface:

```bash
python admin_gui.py
```

**Features:**
- ✅ User-friendly interface
- ✅ Create licenses with form validation
- ✅ View and filter all licenses
- ✅ Revoke licenses with confirmation
- ✅ Real-time Firebase connection status

### Option B: Command Line Tool

Use the CLI for automation and scripting:

```bash
# Create a premium license valid for 1 year
python admin_license_manager.py create \
  --email customer@example.com \
  --app-id spec-updater \
  --plan premium \
  --duration 365 \
  --max-devices 3

# Create an unlimited business license
python admin_license_manager.py create \
  --email business@example.com \
  --app-id coffee-stock-widget \
  --plan business \
  --duration 365 \
  --max-devices -1 \
  --documents-limit -1
```

### Option C: API Integration

For remote license creation from other platforms (website, payment processors):

See **[API_GUIDE.md](API_GUIDE.md)** for:
- REST API setup (Flask)
- Firebase Cloud Functions (production)
- Webhook integrations (Stripe, PayPal)
- Security best practices

### Listing Licenses

```bash
# List all licenses
python admin_license_manager.py list

# List licenses for specific email
python admin_license_manager.py list --email customer@example.com

# List expired licenses
python admin_license_manager.py list --status expired
```

### Revoking Licenses

```bash
python admin_license_manager.py revoke --license ABC123-DEF456-GHI789
```

---

## 7. Security Considerations

### Implemented Security Measures

1. **API Key Protection**
   - API keys stored in separate config file (not in code)
   - Config files excluded from version control

2. **Firestore Security Rules**
   - Clients can only read their own license data
   - Clients cannot modify licenses
   - Only Admin SDK can create/update licenses
   - Device activations are user-specific

3. **License Key Generation**
   - Uses cryptographically secure random generation
   - Format: `XXXXX-XXXXX-XXXXX-XXXXX` (20 characters + hyphens)
   - Collision-resistant (62^20 possible combinations)

4. **Device Binding**
   - Each device gets a unique UUID
   - Stored locally and never transmitted in plain text
   - Limits concurrent device usage

5. **Data Validation**
   - Input validation on client and server
   - Type checking in security rules
   - SQL injection not applicable (NoSQL database)

6. **Rate Limiting** (Recommended to implement)
   - Firebase has built-in quota limits
   - Consider implementing Cloud Functions for additional rate limiting

7. **Audit Logging**
   - All license validations logged with timestamps
   - Usage tracking for compliance

### Additional Security Recommendations

1. **Enable App Check** (Recommended)
   - Protects against automated abuse
   - Go to Firebase Console → App Check
   - Register your app and enable enforcement

2. **Set up Cloud Functions for Validation** (Optional)
   - Move validation logic to server-side Cloud Functions
   - Prevents client-side tampering
   - Requires Firebase Blaze plan (pay-as-you-go)

3. **Regular Security Audits**
   - Review Firestore security rules quarterly
   - Monitor usage logs for anomalies
   - Rotate service account keys annually

4. **Backup Strategy**
   - Regular Firestore exports to Cloud Storage
   - Keep backups of license database

5. **HTTPS Only**
   - Firebase enforces HTTPS by default
   - Never disable SSL verification in production

---

## Testing Your Setup

### 1. Test License Creation

```bash
python admin_license_manager.py create --email test@example.com --plan premium --duration 30
```

### 2. Test License Validation

Run the application and enter the license key when prompted.

### 3. Check Firestore Console

- Go to Firestore Database in Firebase Console
- Verify collections are created
- Check that license documents have correct structure

### 4. Test Security Rules

Try accessing Firestore directly with invalid permissions - should be denied.

---

## Troubleshooting

### "Permission Denied" Errors

- Check that security rules are published
- Verify API key is correct
- Ensure anonymous auth is enabled

### "Service Account Not Found"

- Verify `firebase-admin-key.json` path is correct
- Check file permissions
- Ensure JSON file is valid

### License Validation Fails

- Check internet connection
- Verify Firebase project is active
- Check license expiration date

---

## Next Steps

1. ✅ Complete Firebase project setup
2. ✅ Configure security rules
3. ✅ Install Python dependencies
4. ✅ Create admin service account
5. ✅ Generate first license key
6. ✅ Test validation in application
7. 🔄 (Optional) Set up payment integration (Stripe)
8. 🔄 (Optional) Implement Cloud Functions for advanced validation

---

## Support

For issues with Firebase setup:
- [Firebase Documentation](https://firebase.google.com/docs)
- [Firestore Security Rules Guide](https://firebase.google.com/docs/firestore/security/get-started)
- [Python Admin SDK Reference](https://firebase.google.com/docs/reference/admin/python)
