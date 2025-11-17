# Comprehensive Metrics Tracking System

## Overview

The system now tracks comprehensive user metrics and application usage without requiring user accounts. This enables:
- ✅ User identification across devices (via license key + MAC address)
- ✅ App launch tracking (frequency and timing)
- ✅ Detailed processing session analytics
- ✅ Feature usage tracking
- ✅ Cost analysis and usage patterns

---

## User Identification (Without Accounts)

### How We Identify Users

**Multi-Factor Identification:**
1. **License Key** - Primary identifier (same license = same user)
2. **Device ID** - UUID stored locally (identifies device)
3. **MAC Address** - Physical machine identifier (when available)
4. **User ID** - Hash of (device_id + license_key + MAC) for stable tracking

**Why This Works:**
- Same license on multiple devices → Same user
- Same MAC address → Same physical machine
- Device ID → Unique device installation
- Combined → Stable user identification

**Example:**
```
User on Laptop: device_id=ABC, license=FREE-123, mac=AA:BB:CC
User on Desktop: device_id=XYZ, license=FREE-123, mac=DD:EE:FF

Both map to same user_id (hash of FREE-123)
→ You can track: "User FREE-123 uses app on 2 devices"
```

---

## Metrics Collected

### 1. App Launch Events (`app_launches` collection)

**Tracked on every app startup:**
```json
{
  "app_id": "spec-updater",
  "device_id": "uuid-here",
  "user_id": "stable-hash",
  "license_key": "FREE-XXXX-YYYY",
  "mac_address": "AA:BB:CC:DD:EE:FF",
  "device_info": {
    "machine_name": "WORKSTATION-01",
    "platform": "Windows",
    "platform_version": "10.0.19045",
    "machine": "AMD64",
    "processor": "Intel64 Family 6",
    "python_version": "3.11.0"
  },
  "timestamp": "2025-01-15T10:30:00Z",
  "app_version": "1.0.0",
  "event_type": "app_launch"
}
```

**Queries:**
- How many times app was launched (total, per user, per day)
- Most active users
- Launch frequency patterns

---

### 2. Processing Session Events (`processing_sessions` collection)

**Tracked after each processing run:**

```json
{
  "app_id": "spec-updater",
  "device_id": "uuid-here",
  "user_id": "stable-hash",
  "license_key": "FREE-XXXX-YYYY",
  "mac_address": "AA:BB:CC:DD:EE:FF",
  "device_info": {...},
  "timestamp": "2025-01-15T10:35:00Z",
  "app_version": "1.0.0",
  "event_type": "processing_session",
  
  // Processing Stats
  "files_scanned": 150,
  "documents_updated": 142,
  "documents_with_date_changes": 140,
  "documents_with_phase_changes": 120,
  "documents_with_both": 118,
  "pdfs_created": 45,
  "errors": 2,
  "duration_seconds": 125.5,
  
  // User Actions
  "date_updated": true,
  "phase_updated": true,
  "target_date": "January 15, 2025",
  "phase_text": "100% Construction Documents",
  "recursive": true,
  "dry_run": false,
  
  // Feature Usage
  "include_legacy_doc": true,      // Did they process .doc files?
  "replace_doc_inplace": false,     // Did they replace .doc with .docx?
  "reprint_pdf": true,              // Did they reprint PDFs?
  
  // Backup Settings
  "backup_enabled": true,
  "backup_location_default": false,  // Was default backup location used?
  "backup_path": "D:\\Backups\\Specs\\2025-01-15",
  
  // Excludes
  "exclude_folders_final": ["Archive", "Old", "Draft"],
  
  // Path
  "root_path": "D:\\Projects\\Building-A\\Specs"
}
```

**Queries:**
- Average documents per session
- Most used features
- Backup usage patterns
- Error rates
- Processing time trends

---

## Database Collections

### New Collections

1. **`app_launches`** - Every app startup
2. **`processing_sessions`** - Every processing run

### Updated Collections

1. **`usage_logs`** - Still used for simple document counts
2. **`licenses`** - Enhanced with metrics aggregation

---

## Analytics Examples

### Cost Analysis

```javascript
// Total processing sessions this month
processing_sessions
  .where('timestamp', '>', start_of_month)
  .count()

// Average documents per session
avg(documents_updated) from processing_sessions

// Total PDFs created (for server cost calculation)
sum(pdfs_created) from processing_sessions

// Processing time costs
avg(duration_seconds) * hourly_server_cost
```

### Feature Usage

```javascript
// How many users use legacy .doc support?
count(distinct user_id) where include_legacy_doc = true

// Backup usage
percentage(backup_enabled = true)

// Most common exclude folders
most_frequent(exclude_folders_final)
```

### User Behavior

```javascript
// Daily active users
count(distinct user_id) where timestamp > today

// Sessions per user
group by user_id, count(*)

// Power users (most sessions)
top 10 users by session_count
```

---

## What Gets Tracked

### ✅ Tracked

- **App launches** - Every time app opens
- **Processing sessions** - Every time "Run" is clicked
- **Files scanned** - Total files processed
- **Documents updated** - Successfully modified
- **Date changes** - Documents with date updates
- **Phase changes** - Documents with phase text updates
- **Both changes** - Documents with both
- **PDFs created** - PDFs regenerated
- **Errors** - Processing errors
- **Duration** - Time taken to process
- **Features used** - Which checkboxes were enabled
- **Backup settings** - Whether backup was used and location
- **Exclude folders** - What folders were excluded
- **Device info** - MAC address, machine name, OS, etc.

### ❌ NOT Tracked (Privacy)

- File contents
- Folder names (except excludes list)
- Document text
- Personal information

---

## Privacy & Security

### What's Private

- MAC address is hashed in user_id (can't reverse)
- No file contents logged
- No personal data collected
- Anonymous by default (free licenses)

### What's Public (to you)

- Usage patterns (for cost analysis)
- Feature popularity (for product decisions)
- Error rates (for bug fixes)
- Performance metrics (for optimization)

---

## Implementation Details

### Files Modified

1. **`src/metrics.py`** (NEW)
   - MAC address detection
   - Device fingerprinting
   - User identifier generation

2. **`src/subscription.py`**
   - `log_app_launch()` - Log app startup
   - `log_processing_session()` - Log processing details

3. **`src/main.py`**
   - Updated `UpdateWorker` to track metrics
   - Updated `onFinished()` to log sessions
   - Updated `check_subscription()` to log launches
   - Enhanced `update_docx_dates()` to track what changed

4. **`config/firebase-database-rules.json`**
   - Added rules for `app_launches` and `processing_sessions`

---

## Next Steps

1. ✅ Code implemented - **DONE**
2. ⏳ Deploy database rules to Firebase
3. ⏳ Test metrics logging
4. ⏳ Create analytics dashboard (optional)
5. ⏳ Set up alerts for high error rates (optional)

---

## Query Examples

### Most Active Users (Last 30 Days)

```javascript
// Group by user_id, count sessions
processing_sessions
  .where('timestamp', '>', thirty_days_ago)
  .group_by('user_id')
  .order_by('session_count', desc)
  .limit(10)
```

### Feature Adoption Rate

```javascript
// Percentage using each feature
total_sessions = count(processing_sessions)
with_legacy_doc = count(where include_legacy_doc = true)
with_pdf_reprint = count(where reprint_pdf = true)
with_backup = count(where backup_enabled = true)

legacy_adoption = (with_legacy_doc / total_sessions) * 100
pdf_adoption = (with_pdf_reprint / total_sessions) * 100
backup_adoption = (with_backup / total_sessions) * 100
```

### Average Session Metrics

```javascript
avg(documents_updated)      // 142 docs/session
avg(duration_seconds)       // 125 seconds/session
avg(pdfs_created)           // 45 PDFs/session
avg(errors)                 // 2 errors/session
```

---

## Benefits

1. **Cost Analysis** - Know exactly how much processing costs
2. **Feature Usage** - See what features are actually used
3. **Bug Detection** - Identify error patterns
4. **Performance** - Track processing times
5. **User Behavior** - Understand how users work
6. **Product Decisions** - Data-driven feature development

---

**Everything is logged automatically - no user action required!**

