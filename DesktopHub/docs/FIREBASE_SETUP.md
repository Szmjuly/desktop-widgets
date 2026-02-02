# Firebase Setup Guide for DesktopHub

## Overview

DesktopHub now includes optional Firebase integration for telemetry, licensing, and remote update checking. This integration tracks:

- **Device activations** - Which devices have the app installed
- **Active sessions** - Real-time heartbeat showing active devices
- **Usage analytics** - App launches, session duration, feature usage
- **Error logging** - Exceptions and crashes for debugging
- **Update checking** - Remote version checking for auto-update notifications
- **Free licensing** - Auto-created free licenses for all installs (no enforcement)

## Database Information

**Firebase Project**: `licenses-ff136`  
**Database URL**: `https://licenses-ff136-default-rtdb.firebaseio.com`  
**Service Account**: `firebase-adminsdk-ftaw@licenses-ff136.iam.gserviceaccount.com`

This Firebase database is shared with the Renamer app and uses `app_id` to differentiate between applications.

## Configuration

### Option 1: Service Account File (Recommended for Development)

1. Obtain the `firebase-admin-key.json` service account file
2. Place it in: `%LOCALAPPDATA%\DesktopHub\firebase-config.json`
3. The app will automatically detect and use it

**File location**:
```
C:\Users\<YourUsername>\AppData\Local\DesktopHub\firebase-config.json
```

### Option 2: Embedded Configuration (For Production Builds)

For production builds, embed the service account JSON directly in the `FirebaseService.cs` file:

```csharp
private string? GetEmbeddedServiceAccount()
{
    // Paste the service account JSON here
    return @"{
        ""type"": ""service_account"",
        ""project_id"": ""licenses-ff136"",
        ""private_key_id"": ""..."",
        ""private_key"": ""..."",
        ...
    }";
}
```

### Option 3: Offline Mode (No Configuration)

If no configuration file is found and no embedded config exists, DesktopHub will run in **offline mode** with no telemetry. The app will function normally without Firebase.

## Database Schema

### DesktopHub-Specific Nodes

#### `device_activations/{device_id}`
Tracks device registrations.

```json
{
  "app_id": "desktophub",
  "license_key": "FREE-XXX-YYY",
  "device_id": "uuid",
  "device_name": "Manufacturer Model",
  "activated_at": "2026-01-30T18:00:00Z",
  "last_validated": "2026-01-30T18:00:00Z",
  "app_version": "2.0.0",
  "device_info": {
    "platform": "Win32NT",
    "platform_version": "10.0.19045.0",
    "machine_name": "DESKTOP-ABC123",
    "mac_address": "00:11:22:33:44:55",
    "processor_architecture": "x64"
  }
}
```

#### `device_heartbeats/{device_id}`
Real-time device status (updated every 5 minutes).

```json
{
  "app_id": "desktophub",
  "device_id": "uuid",
  "device_name": "Manufacturer Model",
  "license_key": "FREE-XXX-YYY",
  "last_seen": "2026-01-30T18:00:00Z",
  "status": "active",
  "session_start": "2026-01-30T17:00:00Z",
  "app_version": "2.0.0",
  "device_info": { ... }
}
```

#### `app_launches/{launch_id}`
Records every app launch and close event.

```json
{
  "app_id": "desktophub",
  "device_id": "uuid",
  "user_id": "derived-hash",
  "license_key": "FREE-XXX-YYY",
  "mac_address": "00:11:22:33:44:55",
  "device_info": { ... },
  "timestamp": "2026-01-30T18:00:00Z",
  "app_version": "2.0.0",
  "event_type": "app_launch"
}
```

For app close events, includes:
```json
{
  "event_type": "app_close",
  "session_duration_seconds": 3600
}
```

#### `processing_sessions/{session_id}`
Tracks usage events (widget activations, searches, etc.).

```json
{
  "app_id": "desktophub",
  "device_id": "uuid",
  "user_id": "derived-hash",
  "license_key": "FREE-XXX-YYY",
  "timestamp": "2026-01-30T18:00:00Z",
  "app_version": "2.0.0",
  "event_type": "widget_activated",
  "widget_type": "project_search",
  "search_query_length": 15
}
```

#### `error_logs/{log_id}`
Exception and error tracking.

```json
{
  "app_id": "desktophub",
  "device_id": "uuid",
  "timestamp": "2026-01-30T18:00:00Z",
  "error_type": "NullReferenceException",
  "error_message": "Object reference not set to an instance",
  "stack_trace": "at DesktopHub.UI...",
  "context": "DispatcherUnhandledException",
  "app_version": "2.0.0"
}
```

#### `licenses/{license_key}`
License information (auto-created free licenses).

```json
{
  "license_key": "FREE-ABCD1234-XYZ789",
  "app_id": "desktophub",
  "plan": "free",
  "tier": "free",
  "status": "active",
  "source": "auto-created",
  "created_at": "2026-01-30T18:00:00Z",
  "expires_at": null,
  "max_devices": -1,
  "documents_limit": 0,
  "documents_used": 0,
  "is_bundle": false,
  "email": null
}
```

#### `app_versions/desktophub` (Admin-Managed)
Version information for update checking.

```json
{
  "latest_version": "2.0.1",
  "release_date": "2026-02-01T12:00:00Z",
  "release_notes": "Bug fixes and performance improvements",
  "download_url": "https://github.com/user/repo/releases/download/v2.0.1/DesktopHub.exe",
  "required_update": false
}
```

#### `update_checks/{check_id}`
Logs when devices check for updates.

```json
{
  "app_id": "desktophub",
  "device_id": "uuid",
  "current_version": "2.0.0",
  "latest_version": "2.0.1",
  "update_available": true,
  "timestamp": "2026-01-30T18:00:00Z"
}
```

## Security Rules

The existing Firebase security rules already support DesktopHub. The rules are app-agnostic and use `app_id` for filtering.

**Key Rules**:
- `device_activations`: Readable/writable by authenticated devices
- `device_heartbeats`: Readable/writable by authenticated devices
- `app_launches`: Write-only (no read access)
- `processing_sessions`: Write-only
- `error_logs`: Write-only
- `licenses`: Readable if you own the license
- `update_checks`: Write-only

## Authentication

DesktopHub uses the **Firebase Admin SDK** with a service account, which means:

✅ **No anonymous user accounts** - Cleaner than the Renamer implementation  
✅ **No user authentication required** - Service account has full access  
✅ **Simpler code** - Direct database access without tokens  
✅ **Better for desktop apps** - No need for user login flows  

**Note**: The service account key must be kept secure and not committed to version control.

## Device Identification

Each device is assigned a unique UUID on first launch, stored at:
```
%LOCALAPPDATA%\DesktopHub\device_id.txt
```

This ensures the same device always has the same ID across app restarts.

Device fingerprint includes:
- Platform (Windows version)
- Machine name
- MAC address (hashed for privacy)
- Processor architecture

## Heartbeat System

When the app is running:
- **Initial heartbeat**: Sent on app launch
- **Periodic heartbeat**: Every 5 minutes
- **Final heartbeat**: On app close (marks device as inactive)

This allows real-time monitoring of active devices in the Firebase console.

## Usage Tracking

To log custom usage events:

```csharp
// In your UI code
var firebaseManager = App.Current.FirebaseManager; // Add public property to App
await firebaseManager.LogUsageEventAsync("widget_activated", new Dictionary<string, object>
{
    ["widget_type"] = "project_search",
    ["search_query"] = "redacted",
    ["results_count"] = 42
});
```

## Update Checking

The app checks for updates on startup and via the tray menu "Check for Updates" option.

To push a new version:
1. Update `app_versions/desktophub` in Firebase
2. Set `latest_version` to the new version number
3. Optionally set `required_update: true` for critical updates

Users will see a notification if an update is available.

## Licensing System

### Current Behavior (Free Mode)

Currently, licensing is **enabled** but in "free mode":
- Every device gets a free license auto-created on first launch
- License key format: `FREE-{device-hash}-{random}`
- No validation or enforcement
- Still tracks usage and activations

### True "Licensing Disabled" Mode

To completely disable licensing (no license creation at all):

1. Comment out license creation in `FirebaseService.cs`:
   ```csharp
   public async Task<bool> EnsureLicenseExistsAsync()
   {
       // Disable auto-license creation
       return true;
   }
   ```

2. Set license key to null in all tracking calls

**Note**: Even with licensing disabled, you still get usage analytics and device tracking.

## Data Retention

### Current Status
❌ **No automatic cleanup** - Data grows indefinitely

### Recommended Implementation

Add Firebase Cloud Functions to archive/delete old data:

1. **Archive logs after 90 days** to Cloud Storage
2. **Delete error logs after 6 months**
3. **Keep device activations indefinitely**
4. **Mark inactive devices** (no heartbeat for 30 days)

**Cloud Function Example** (not yet implemented):
```javascript
exports.cleanupOldLogs = functions.pubsub.schedule('every 24 hours').onRun(async (context) => {
  const cutoff = Date.now() - (90 * 24 * 60 * 60 * 1000); // 90 days ago
  const snapshot = await admin.database().ref('error_logs')
    .orderByChild('timestamp')
    .endAt(cutoff)
    .once('value');
  
  // Archive to Cloud Storage, then delete
  // ... implementation
});
```

## Privacy Considerations

**Data Collected**:
- Device UUID (unique ID, not personally identifiable)
- Machine name (can be anonymized if desired)
- MAC address (can be hashed)
- IP address (collected by Firebase automatically)
- Search queries (can be anonymized/redacted)
- Error stack traces

**Not Collected**:
- User names or email addresses (unless explicitly added)
- File paths or project names (should be redacted)
- Passwords or credentials

**Recommendations**:
1. Anonymize search queries before logging
2. Hash or redact sensitive file paths in error logs
3. Document data collection in privacy policy
4. Provide opt-out mechanism if required

## Troubleshooting

### Firebase not initializing
**Check**:
1. Configuration file exists at `%LOCALAPPDATA%\DesktopHub\firebase-config.json`
2. Service account JSON is valid
3. Database URL is correct
4. Check `logs\debug.log` for errors

### Heartbeat not updating
**Check**:
1. Firebase is initialized (`IsInitialized` = true)
2. App is not in offline mode
3. Timer is running (check logs)
4. Network connectivity

### Errors not logging
**Possible causes**:
1. Firebase not initialized
2. Network issues
3. Security rules blocking writes
4. Invalid JSON data

### Update checking not working
**Check**:
1. `app_versions/desktophub` node exists in Firebase
2. Node has `latest_version` field
3. Version format is valid (e.g., "2.0.1")

## Firebase Console Access

**URL**: https://console.firebase.google.com/project/licenses-ff136

**Views**:
- **Realtime Database** → See all data in real-time
- **Authentication** → Anonymous users (not used by DesktopHub)
- **Service Accounts** → Manage admin SDK credentials

**Useful Queries** (in Database rules playground):
```javascript
// Find all active DesktopHub devices
root.child('device_heartbeats').orderByChild('app_id').equalTo('desktophub')

// Find recent errors
root.child('error_logs').orderByChild('app_id').equalTo('desktophub').limitToLast(100)

// Count active licenses
root.child('licenses').orderByChild('app_id').equalTo('desktophub')
```

## Next Steps

1. ✅ Copy service account key to DesktopHub config location
2. ✅ Build and test Firebase integration
3. ⚠️ Add update checking to tray menu
4. ⚠️ Implement data retention policies
5. ⚠️ Add widget usage tracking
6. ⚠️ Add privacy opt-out option (if needed)
7. ⚠️ Document data collection in user-facing docs

## Comparison with Renamer Implementation

| Feature | Renamer (Python) | DesktopHub (C#) |
|---------|-----------------|-----------------|
| **SDK** | Pyrebase + Admin SDK | Admin SDK only |
| **Auth** | Anonymous auth | Service account |
| **User accounts** | Many anonymous users | No users |
| **Complexity** | Higher (2 SDKs) | Lower (1 SDK) |
| **device_activations key** | Auth UID | Device UUID |
| **Code clarity** | Mixed patterns | Consistent |
| **Licensing** | Free auto-created | Free auto-created |

**DesktopHub Improvements**:
- ✅ No anonymous user clutter
- ✅ Consistent device identification
- ✅ Cleaner authentication pattern
- ✅ Better separation of concerns
- ✅ Async/await throughout
