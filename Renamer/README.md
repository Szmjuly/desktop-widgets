# Multi-Application License Management System

A professional, centralized Firebase-based subscription system that manages licenses for multiple applications from a single Firebase project.

## Supported Applications

- **Spec Header Date Updater** (`spec-updater`) - Document management tool
- **Coffee Stock Widget** (`coffee-stock-widget`) - Desktop widget for inventory
- **Your Next App** - Easily add more applications!

## Features

- **Document Processing**: Automatically updates dates in Word document headers
- **Batch Processing**: Process multiple documents recursively
- **Subscription Management**: Secure Firebase-based license validation
- **Usage Tracking**: Monitor document processing and quota limits
- **Multi-device Support**: License can be used on multiple devices (based on plan)
- **Secure**: Industry-standard security practices with Firebase

## Quick Start

### 1. Install Dependencies

```bash
pip install -r requirements.txt
```

### 2. Set Up Firebase

Follow the complete setup guide in [FIREBASE_SETUP.md](FIREBASE_SETUP.md).

Quick checklist:
- [ ] Create Firebase project
- [ ] Enable Realtime Database
- [ ] Enable Anonymous Authentication
- [ ] Download service account key
- [ ] Get Web API key
- [ ] Create `firebase_config.json` from template
- [ ] Deploy security rules

### 3. Create Your First License

For **Spec Header Date Updater**:
```bash
python admin_license_manager.py create \
  --email your@email.com \
  --app-id spec-updater \
  --plan premium \
  --duration 365
```

For **Coffee Stock Widget**:
```bash
python admin_license_manager.py create \
  --email your@email.com \
  --app-id coffee-stock-widget \
  --plan premium \
  --duration 365
```

This will output a license key like: `ABCDE-12345-FGHIJ-67890`

### 4. Run the Application

**Option A: Interactive Menu** (Recommended)
```bash
python launcher.py
```

**Option B: Direct Launch**
```bash
python run_app.py
```

Enter your license key when prompted.

## Project Structure

```
Renamer/
├── launcher.py                      # 🚀 Main launcher menu
├── run_app.py                       # Quick app launcher
├── run_admin_gui.py                 # Quick admin launcher
├── requirements.txt                 # Python dependencies
│
├── src/                             # Source code
│   ├── main.py                      # Main application
│   └── subscription.py              # License management
│
├── admin/                           # Admin tools
│   ├── admin_gui.py                 # GUI license manager ⭐
│   ├── admin_license_manager.py     # CLI license manager
│   └── toggle_subscription.py       # Subscription toggle
│
├── tests/                           # Test suite
│   ├── test_security.py             # Security tests
│   └── test_firebase_import.py      # Diagnostics
│
├── config/                          # Configuration templates
│   ├── firebase_config.example.json
│   ├── app_config.example.json
│   └── firebase-database-rules.json
│
└── docs/                            # Documentation
    ├── FIREBASE_SETUP.md
    ├── QUICKSTART.md
    ├── API_GUIDE.md
    └── ... (more guides)
```

See [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) for complete structure details.

## Subscription Plans

### Free (Limited)
- Basic document processing
- 10 documents per month
- 1 device
- Community support

### Premium
- Unlimited documents
- 3 devices
- Legacy .doc support
- PDF export
- Priority support

### Business
- Unlimited documents
- Unlimited devices
- All premium features
- API access
- Dedicated support

## Admin Tools

### GUI Admin Tool (Recommended)

Launch the graphical interface for easy license management:

```bash
python run_admin_gui.py
# Or
python launcher.py  # Choose option 2
```

**Features:**
- 🎨 User-friendly interface
- 📝 Create licenses with form validation
- 📊 View and filter licenses by app, email, or status
- 🗑️ Revoke licenses with confirmation dialogs
- ⚡ Real-time Firebase connection status
- 🔄 Refresh license list on demand

**Perfect for:** Non-technical staff, daily admin tasks

### CLI Commands

For automation, scripting, and integration:

### Create License
```bash
# For Spec Updater
python admin/admin_license_manager.py create \
  --email customer@example.com \
  --app-id spec-updater \
  --plan premium \
  --duration 365 \
  --max-devices 3

# For Coffee Widget
python admin/admin_license_manager.py create \
  --email customer@example.com \
  --app-id coffee-stock-widget \
  --plan premium \
  --duration 365 \
  --max-devices 3
```

### List All Licenses
```bash
# All licenses across all apps
python admin/admin_license_manager.py list

# Only Spec Updater licenses
python admin/admin_license_manager.py list --app-id spec-updater

# Only Coffee Widget licenses
python admin/admin_license_manager.py list --app-id coffee-stock-widget
```

### Filter Licenses by Email
```bash
python admin/admin_license_manager.py list --email customer@example.com
```

### Get License Details
```bash
python admin/admin_license_manager.py info --license ABCDE-12345-FGHIJ-67890
```

### Extend License
```bash
python admin/admin_license_manager.py extend \
  --license ABCDE-12345-FGHIJ-67890 \
  --days 90
```

### Revoke License
```bash
python admin/admin_license_manager.py revoke --license ABCDE-12345-FGHIJ-67890
```

### API Integration

For remote license creation from other platforms (e.g., your website, Stripe webhooks):

See **[API_GUIDE.md](API_GUIDE.md)** for complete documentation on:

- **Flask REST API** - Simple API with key-based authentication
- **Firebase Cloud Functions** - Production-ready serverless API
- **Webhook Integration** - Stripe, PayPal, and other payment processors
- **Security Best Practices** - Authentication, rate limiting, validation

**Example Use Cases:**
- Automatically create licenses after payment
- Integrate with your e-commerce platform
- Allow partner resellers to generate licenses
- Build custom admin dashboards

**Is it secure?** Yes, when properly implemented! The API guide covers:
- ✅ Authentication methods (API keys, JWT, Firebase Auth)
- ✅ HTTPS enforcement
- ✅ Rate limiting
- ✅ Input validation
- ✅ Audit logging

## Security Features

### Implemented Protections

1. **Firebase Security Rules**
   - Read-only license data for clients
   - Device-specific activation tracking
   - Server-side validation required for license creation

2. **Secure Key Generation**
   - Cryptographically secure random generation
   - 62^20 possible combinations (collision-resistant)
   - Format: `XXXXX-XXXXX-XXXXX-XXXXX`

3. **Device Binding**
   - Unique device UUID per installation
   - Prevents unauthorized license sharing
   - Tracks active devices per license

4. **Anonymous Authentication**
   - Firebase Anonymous Auth for secure API access
   - No personal data stored in auth
   - Device-specific tokens

5. **Local Caching**
   - Encrypted local subscription cache
   - Reduces Firebase queries
   - Offline validation for short periods

6. **Audit Logging**
   - All license validations logged
   - Usage tracking per device
   - Timestamp-based analytics

### Protection Against Common Attacks

- **SQL Injection**: Not applicable (NoSQL database)
- **XSS**: Not applicable (desktop application)
- **License Duplication**: Device binding + max device limits
- **Replay Attacks**: Timestamp validation + token expiration
- **Man-in-the-Middle**: Firebase enforces HTTPS
- **Tampering**: Server-side validation, client cannot modify licenses

## Configuration

### Environment Variables

Create a `.env` file (optional):

```env
FIREBASE_ADMIN_KEY_PATH=firebase-admin-key.json
FIREBASE_CONFIG_PATH=firebase_config.json
APP_VERSION=1.0.0
```

### Firebase Config

Create `firebase_config.json` from the template:

```json
{
  "apiKey": "YOUR_API_KEY",
  "authDomain": "your-project.firebaseapp.com",
  "projectId": "your-project-id",
  "storageBucket": "your-project.appspot.com",
  "databaseURL": "https://your-project-default-rtdb.firebaseio.com"
}
```

## Usage

### For End Users

1. Launch the application
2. Enter license key when prompted
3. Select folder containing documents
4. Choose processing options
5. Click "Run"

### For Administrators

1. Create licenses using admin tool
2. Monitor usage in Firebase Console
3. Manage customer subscriptions
4. Review usage logs and analytics

## Troubleshooting

### "Firebase config not found"
- Ensure `firebase_config.json` exists
- Check file path and permissions
- Verify JSON format is valid

### "License key not found"
- Verify license exists in Firebase
- Check license key spelling
- Ensure license hasn't been revoked

### "Maximum device limit reached"
- Upgrade to higher tier plan
- Deactivate unused devices in Firebase Console
- Contact support for assistance

### "Permission denied" in Firebase
- Check Firebase security rules are deployed
- Verify Anonymous Auth is enabled
- Ensure API key is correct

## Development

### Running Tests

```bash
# Test license creation
python admin_license_manager.py create --email test@test.com --plan premium --duration 30

# Test validation
python update_spec_header_dates_v2.py
```

### Adding New Features

1. Update `subscription.py` for new validation logic
2. Modify Firebase security rules if needed
3. Update admin tool for new license properties
4. Test thoroughly before deployment

## Best Practices

### For Security

1. **Never commit** `firebase_config.json` or `firebase-admin-key.json`
2. **Rotate service account keys** annually
3. **Monitor Firebase usage** for anomalies
4. **Review security rules** quarterly
5. **Enable App Check** for production
6. **Use HTTPS only** (Firebase default)

### For Performance

1. **Cache subscription data** locally
2. **Batch Firebase queries** when possible
3. **Use indexes** for common queries
4. **Monitor quota limits** in Firebase Console

### For Reliability

1. **Regular backups** of Firebase data
2. **Test license validation** before releases
3. **Handle offline scenarios** gracefully
4. **Log errors** for debugging

## Support

### Documentation
- [Multi-App Guide](MULTI_APP_GUIDE.md) - **Managing multiple applications**
- [Firebase Setup Guide](FIREBASE_SETUP.md) - Complete Firebase configuration
- [Quick Start](QUICKSTART.md) - Get started in 5 steps
- [Security Documentation](SECURITY.md) - Security architecture
- [Testing Guide](TESTING.md) - **Security test suite and validation**
- [API Guide](API_GUIDE.md) - Remote license creation API
- [Admin Tools](ADMIN_TOOLS_SUMMARY.md) - GUI, CLI, and API comparison
- [Firebase Documentation](https://firebase.google.com/docs)
- [Python Admin SDK](https://firebase.google.com/docs/reference/admin/python)

### Subscription Enforcement

Control whether users can use the app without a license:

```bash
# Check current mode
python admin/toggle_subscription.py status

# Enforce licenses (production) - Users MUST have valid license
python admin/toggle_subscription.py enable

# Make optional (testing) - Users can cancel license dialog
python admin/toggle_subscription.py disable
```

**Default:** Enforced (users cannot use app without valid license)

See [docs/SUBSCRIPTION_ENFORCEMENT.md](docs/SUBSCRIPTION_ENFORCEMENT.md) for complete guide.

### Testing

Run the comprehensive security test suite:

```bash
# Run all security tests
python tests/test_security.py

# Or use launcher
python launcher.py  # Choose option 4

# Expected output: 12/12 tests passed
# Tests: Random keys, brute force, injections, validation, etc.
```

See [docs/TESTING.md](docs/TESTING.md) for detailed test documentation.

### Common Issues
- Check `.gitignore` is properly configured
- Verify Python version (3.8+ recommended)
- Ensure all dependencies are installed
- Review Firebase Console for errors

## License

Copyright © 2025. All rights reserved.

This software requires a valid license key for operation. Unauthorized use, distribution, or reverse engineering is prohibited.

## Changelog

### Version 1.0.0
- Initial release with Firebase integration
- Subscription management system
- Admin tools for license management
- Secure device binding
- Usage tracking and analytics
