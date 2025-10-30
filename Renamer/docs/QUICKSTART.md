# Quick Start Guide - Firebase Subscription System

## 🚀 Get Started in 5 Steps

### Step 1: Install Dependencies
```bash
pip install -r requirements.txt
```

### Step 2: Run Setup Script
```bash
python setup.py
```

This will:
- Check dependencies
- Create `.gitignore` 
- Guide you through Firebase config creation

### Step 3: Configure Firebase (First Time Only)

#### A. Create Firebase Project
1. Go to https://console.firebase.google.com/
2. Click "Create a project"
3. Name it: **`licenses`** (This ONE project will handle ALL your applications)
4. Disable Google Analytics (optional)

**Note:** This single Firebase project manages licenses for all your apps (Spec Updater, Coffee Widget, etc.).

#### B. Enable Realtime Database
1. In Firebase Console, click "Realtime Database"
2. Click "Create Database"
3. Choose location (e.g., `us-central1`)
4. Start in **production mode**

#### C. Enable Anonymous Authentication
1. Click "Authentication" → "Get Started"
2. Click "Sign-in method" tab
3. Enable "Anonymous" authentication
4. Save

#### D. Deploy Security Rules
1. In Realtime Database, click "Rules" tab
2. Copy content from `firebase-database-rules.json`
3. Paste into the rules editor
4. Click "Publish"

#### E. Download Service Account Key
1. Click Settings ⚙️ → "Project settings"
2. Go to "Service accounts" tab
3. Click "Generate new private key"
4. Save as `firebase-admin-key.json` in the Renamer folder
5. **NEVER commit this file to Git**

### Step 4: Create Your First License

**Option A: GUI Tool (Easiest)**

```bash
python admin_gui.py
```

1. Fill in the form:
   - Email: `your@email.com`
   - Application: `spec-updater`
   - Plan: `premium`
   - Duration: `365` days
2. Click "Create License"
3. Copy the license key that appears

**Option B: Command Line**

For **Spec Header Date Updater**:
```bash
python admin_license_manager.py create \
  --email your@email.com \
  --app-id spec-updater \
  --plan premium \
  --duration 365
```

For **Coffee Stock Widget** (or other apps):
```bash
python admin_license_manager.py create \
  --email your@email.com \
  --app-id coffee-stock-widget \
  --plan premium \
  --duration 365
```

Copy the license key that's displayed (e.g., `ABCDE-12345-FGHIJ-67890`)

### Step 5: Run the Application
```bash
python update_spec_header_dates_v2.py
```

Enter the license key when prompted.

---

## 📋 File Checklist

After setup, you should have:

- ✅ `firebase_config.json` (created by setup.py)
- ✅ `firebase-admin-key.json` (downloaded from Firebase)
- ✅ `.gitignore` (prevents committing sensitive files)
- ✅ All Python dependencies installed

**Never commit:**
- ❌ `firebase_config.json`
- ❌ `firebase-admin-key.json`
- ❌ `.env`

---

## 🔑 Common Admin Commands

### Create License
```bash
# Premium for Spec Updater (3 devices, unlimited docs)
python admin_license_manager.py create --email user@example.com --app-id spec-updater --plan premium --duration 365

# Business for Coffee Widget (unlimited devices)
python admin_license_manager.py create --email biz@example.com --app-id coffee-stock-widget --plan business --duration 365 --max-devices -1
```

### List All Licenses
```bash
# List all licenses across all apps
python admin_license_manager.py list

# List only Spec Updater licenses
python admin_license_manager.py list --app-id spec-updater

# List only Coffee Widget licenses
python admin_license_manager.py list --app-id coffee-stock-widget
```

### Get License Info
```bash
python admin_license_manager.py info --license ABCDE-12345-FGHIJ-67890
```

### Extend License
```bash
python admin_license_manager.py extend --license ABCDE-12345-FGHIJ-67890 --days 90
```

### Revoke License
```bash
python admin_license_manager.py revoke --license ABCDE-12345-FGHIJ-67890
```

---

## 🔒 Security Checklist

- ✅ Firebase security rules deployed
- ✅ Anonymous auth enabled (for device authentication)
- ✅ Sensitive files in `.gitignore`
- ✅ Service account key stored securely
- ✅ HTTPS enforced (Firebase default)
- ✅ Device binding enabled
- ✅ Usage logging active

**Optional (Recommended for Production):**
- ⬜ Enable Firebase App Check
- ⬜ Set up rate limiting with Cloud Functions
- ⬜ Configure backup strategy
- ⬜ Set up monitoring alerts

---

## 🐛 Troubleshooting

### "Firebase config not found"
→ Run `python setup.py` again

### "Permission denied" errors
→ Check that security rules are deployed in Firebase Console

### "License key not found"
→ Verify license was created: `python admin_license_manager.py list`

### "Module not found" errors
→ Install dependencies: `pip install -r requirements.txt`

### "Maximum device limit reached"
→ Check active devices: `python admin_license_manager.py info --license YOUR-KEY`

---

## 📚 Documentation

- **FIREBASE_SETUP.md** - Detailed Firebase configuration
- **README.md** - Complete feature list and admin guide
- **requirements.txt** - Python dependencies

---

## 🆘 Need Help?

1. Check documentation files
2. Review Firebase Console for errors
3. Check application logs
4. Verify all setup steps completed

---

## ✨ What's Next?

After basic setup:

1. **Test the system** - Create a test license and validate it
2. **Configure plans** - Adjust pricing tiers in admin script
3. **Customize branding** - Update URLs and help links in code
4. **Set up payments** (Optional) - Integrate Stripe or PayPal
5. **Deploy to production** - Package application for distribution

---

## 🎯 Production Deployment

When ready for production:

1. ✅ Test thoroughly with multiple licenses
2. ✅ Set up automatic backups of Firebase
3. ✅ Configure monitoring and alerts
4. ✅ Document customer onboarding process
5. ✅ Set up customer support system
6. ✅ Create license purchase workflow
7. ✅ Test license validation edge cases

---

**Ready to build? Start with Step 1! 🚀**
