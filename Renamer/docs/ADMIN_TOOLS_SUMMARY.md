# Admin Tools Summary

## Overview

You now have **3 ways** to manage licenses:

1. **GUI Tool** (`admin_gui.py`) - User-friendly interface ⭐ **Recommended**
2. **CLI Tool** (`admin_license_manager.py`) - Command line for automation
3. **API** (optional) - Remote license creation from other platforms

---

## 1. GUI Admin Tool ⭐

### Launch
```bash
python admin_gui.py
```

### Features

#### Create License Tab
- **Visual form** with dropdowns and validation
- **Application selector** (spec-updater, coffee-stock-widget, or custom)
- **Plan selection** (free, basic, premium, business)
- **Duration spinner** (1-3650 days)
- **Device limits** with "unlimited" checkbox
- **Document limits** with "unlimited" checkbox
- **Real-time results** with success/error messages
- **Auto-copy** license key on creation

#### Manage Licenses Tab
- **Filter by:**
  - Email address
  - Application ID
  - Status (active, expired, suspended)
- **Table view** showing:
  - License key (truncated)
  - App ID
  - Email
  - Plan
  - Status
  - Expiration date
- **Actions:**
  - Info button - View full license details and active devices
  - Revoke button - Suspend license with confirmation
- **Refresh button** - Reload licenses from Firebase

#### Status Bar
- ✅ **Connection indicator** - Shows Firebase connection status
- 📊 **Real-time updates** - Reflects current operation status

### Screenshots

```
┌─────────────────────────────────────────────────┐
│     🔑 License Management System                │
├─────────────────────────────────────────────────┤
│ ✅ Connected to Firebase                        │
├─────────────────────────────────────────────────┤
│  [ Create License ] [ Manage Licenses ]         │
│                                                  │
│  License Details                                │
│  ┌──────────────────────────────────────────┐  │
│  │ Customer Email: [customer@example.com  ] │  │
│  │ Application:    [spec-updater        ▼] │  │
│  │ Plan:           [premium             ▼] │  │
│  │ Duration:       [365] days              │  │
│  │ Max Devices:    [3]  ☐ Unlimited        │  │
│  │ Docs Limit:     [-1] ☑ Unlimited        │  │
│  └──────────────────────────────────────────┘  │
│                                                  │
│       [ Create License ]                        │
│                                                  │
│  Result:                                        │
│  ┌──────────────────────────────────────────┐  │
│  │ ✅ License created successfully!         │  │
│  │                                           │  │
│  │ License Key: ABCDE-12345-FGHIJ-67890     │  │
│  └──────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

### Benefits
- ✅ No command line knowledge needed
- ✅ Perfect for non-technical staff
- ✅ Visual feedback on errors
- ✅ Prevents common mistakes with validation
- ✅ Easy to see all licenses at a glance

---

## 2. CLI Admin Tool

### Launch
```bash
python admin_license_manager.py [command] [options]
```

### Commands

#### Create License
```bash
python admin_license_manager.py create \
  --email customer@example.com \
  --app-id spec-updater \
  --plan premium \
  --duration 365 \
  --max-devices 3 \
  --documents-limit -1
```

**Options:**
- `--email` (required) - Customer email address
- `--app-id` (required) - Application identifier
- `--plan` - License plan: free, basic, premium, business (default: premium)
- `--duration` - Days until expiration (default: 365)
- `--max-devices` - Max devices (-1 for unlimited, default: 3)
- `--documents-limit` - Monthly docs (-1 for unlimited, default: -1)

#### List Licenses
```bash
# All licenses
python admin_license_manager.py list

# Filter by app
python admin_license_manager.py list --app-id spec-updater

# Filter by email
python admin_license_manager.py list --email customer@example.com

# Filter by status
python admin_license_manager.py list --status active

# Combine filters
python admin_license_manager.py list --app-id coffee-stock-widget --status expired
```

#### Get License Info
```bash
python admin_license_manager.py info --license ABCDE-12345-FGHIJ-67890
```

Shows:
- All license details
- Active devices with names and IDs
- Last validation timestamps

#### Extend License
```bash
python admin_license_manager.py extend \
  --license ABCDE-12345-FGHIJ-67890 \
  --days 90
```

#### Revoke License
```bash
python admin_license_manager.py revoke --license ABCDE-12345-FGHIJ-67890
```

### Benefits
- ✅ Perfect for automation and scripts
- ✅ Can be called from other programs
- ✅ Easy to integrate with CI/CD pipelines
- ✅ Batch operations via shell scripts
- ✅ Logging and output redirection

### Use Cases
- **Automation scripts** - Create licenses in bulk
- **Cron jobs** - Regular license expiration checks
- **Integration** - Call from other applications
- **Testing** - Quick license creation for QA

---

## 3. API Integration (Optional)

For creating licenses remotely from other platforms.

### When to Use
- ✅ Website checkout - Create license after payment
- ✅ Payment webhooks - Stripe, PayPal, etc.
- ✅ Reseller portals - Allow partners to generate licenses
- ✅ Custom dashboards - Build your own admin interface
- ✅ Mobile apps - Remote license management

### Security Levels

| Method | Security | Use Case |
|--------|----------|----------|
| **Flask API + API Keys** | ⚠️ Basic | Internal tools, trusted webhooks |
| **Firebase Cloud Functions** | ✅✅ High | Production, public APIs |
| **OAuth 2.0** | ✅✅✅ Maximum | Enterprise deployments |

### Quick Example (Flask)

```python
# Create license via API
import requests

response = requests.post(
    'https://your-api.com/api/v1/licenses',
    headers={'X-API-Key': 'your-secret-key'},
    json={
        'email': 'customer@example.com',
        'app_id': 'spec-updater',
        'plan': 'premium',
        'duration_days': 365
    }
)

license_key = response.json()['license_key']
```

### Documentation
See **[API_GUIDE.md](API_GUIDE.md)** for complete setup instructions.

---

## Comparison Table

| Feature | GUI Tool | CLI Tool | API |
|---------|----------|----------|-----|
| **Ease of Use** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ |
| **Visual Interface** | ✅ | ❌ | ❌ |
| **Automation** | ❌ | ✅ | ✅✅ |
| **Remote Access** | ❌ | ❌ | ✅ |
| **Batch Operations** | ❌ | ✅ | ✅ |
| **Real-time Feedback** | ✅ | ✅ | ✅ |
| **View All Licenses** | ✅ | ✅ | ✅ |
| **Filter/Search** | ✅ | ✅ | ✅ |
| **Setup Complexity** | ⭐ | ⭐ | ⭐⭐⭐⭐ |
| **Security** | ✅✅ | ✅✅ | ✅ (depends) |

---

## Which Tool Should I Use?

### Use GUI Tool When:
- ✅ You're doing manual license management
- ✅ Non-technical staff need to create licenses
- ✅ You want visual confirmation of actions
- ✅ You need to browse and filter licenses
- ✅ Daily administrative tasks

### Use CLI Tool When:
- ✅ Automating license creation
- ✅ Running scripts or batch operations
- ✅ Integrating with existing shell scripts
- ✅ Need simple, scriptable commands
- ✅ Testing and development

### Use API When:
- ✅ Creating licenses from your website
- ✅ Payment processor webhooks (Stripe, PayPal)
- ✅ Building custom admin dashboards
- ✅ Allowing resellers to generate licenses
- ✅ Mobile app integration
- ✅ Multi-platform support needed

---

## Quick Start Guide

### For Daily Admin Tasks
```bash
# Launch GUI
python admin_gui.py

# Create license in GUI
# View/manage licenses in GUI
```

### For Automation
```bash
# Create license via CLI
python admin_license_manager.py create \
  --email user@example.com \
  --app-id spec-updater \
  --plan premium \
  --duration 365

# List licenses
python admin_license_manager.py list --app-id spec-updater
```

### For Integration
```bash
# Set up Flask API
pip install flask flask-cors
python license_api.py

# Or deploy Cloud Functions
firebase deploy --only functions
```

---

## Security Best Practices

### All Tools

1. **Protect credentials**
   - Never commit `firebase-admin-key.json`
   - Keep `firebase_config.json` private
   - Use `.env` for sensitive data

2. **Audit logging**
   - All tools log operations
   - Review logs regularly
   - Track who created what

3. **Access control**
   - Limit who can run admin tools
   - Use separate accounts for automation
   - Revoke access when staff leave

### API Specific

4. **Authentication required**
   - Never expose without auth
   - Rotate API keys regularly
   - Use strong secrets

5. **Rate limiting**
   - Prevent abuse
   - Set reasonable limits
   - Monitor unusual activity

6. **HTTPS only**
   - Never use HTTP
   - Verify certificates
   - Use modern TLS versions

---

## Troubleshooting

### GUI Won't Start

**Problem:** `ModuleNotFoundError: No module named 'PySide6'`  
**Solution:** `pip install PySide6`

**Problem:** "Firebase config not found"  
**Solution:** Create `firebase_config.json` from template

**Problem:** "Admin key not found"  
**Solution:** Download `firebase-admin-key.json` from Firebase Console

### CLI Issues

**Problem:** "License creation failed"  
**Solution:** Check Firebase connection and credentials

**Problem:** "Invalid app_id"  
**Solution:** Use one of: `spec-updater`, `coffee-stock-widget`, or custom ID

### API Issues

**Problem:** "Permission denied"  
**Solution:** Check API key and authentication

**Problem:** "CORS error"  
**Solution:** Enable CORS in Flask (`flask-cors`)

---

## Summary

✨ **GUI Tool** - Best for daily use, easy for everyone  
⚡ **CLI Tool** - Best for automation and scripts  
🌐 **API** - Best for remote integration

**Start with the GUI** for immediate use, add CLI for automation, and implement API when you need remote access.

All tools use the same Firebase backend, so you can mix and match as needed!
