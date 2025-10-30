# Troubleshooting Guide

## Common Issues and Solutions

### 1. Firebase Import Error

**Error:**
```
ImportError: Firebase libraries not installed
```

**Solution:**
```bash
pip install firebase-admin
```

**Note:** The app now works with just `firebase-admin`. Pyrebase is optional.

---

### 2. Windows Long Path Error

**Error:**
```
OSError: [Errno 2] No such file or directory: 'C:\...\very\long\path\...'
HINT: Enable Windows Long Path support
```

**Solutions:**

#### Option A: Enable Long Paths (Recommended)
1. Run as Administrator in PowerShell:
```powershell
New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" `
                 -Name "LongPathsEnabled" -Value 1 -PropertyType DWORD -Force
```
2. Restart your computer
3. Run `pip install -r requirements.txt` again

#### Option B: Use Shorter Path
Move your project to a shorter path like `C:\dev\widgets\`

---

### 3. firebase-admin-key.json Not Found

**Error:**
```
Admin key not found: C:\...\firebase-admin-key.json
```

**Solution:**

The app checks these locations:
1. `desktop-widgets\Renamer\firebase-admin-key.json` ✅ Recommended
2. `desktop-widgets\firebase-admin-key.json` ✅ Also works

Move your key to one of these locations:
```powershell
move ..\firebase-admin-key.json .
```

---

### 4. firebase_config.json Not Found

**Error:**
```
Firebase config not found
```

**Solution:**

1. Copy the example file:
```powershell
copy firebase_config.example.json firebase_config.json
```

2. Edit `firebase_config.json` with your Firebase project details:
```json
{
  "apiKey": "YOUR_API_KEY",
  "authDomain": "your-project.firebaseapp.com",
  "projectId": "your-project-id",
  "storageBucket": "your-project.appspot.com",
  "databaseURL": "https://your-project-default-rtdb.firebaseio.com"
}
```

Get these values from Firebase Console → Project Settings → General → Your apps

---

### 5. PySide6 Installation Issues

**Error:**
```
ERROR: Could not install PySide6
```

**Solutions:**

#### Option A: Install without PySide6 first
```bash
pip install firebase-admin python-docx psutil
```

#### Option B: Enable Long Paths (see #2 above)

#### Option C: Install PySide6 separately
```bash
pip install --no-cache-dir PySide6
```

---

### 6. Module Import Errors

**Error:**
```
ModuleNotFoundError: No module named 'subscription'
```

**Solution:**

Make sure you're in the right directory:
```powershell
cd C:\Users\smarkowitz\repos\desktop-widgets\Renamer
```

And your venv is activated:
```powershell
.\venv\Scripts\Activate.ps1
```

---

### 7. Firebase Connection Errors

**Error:**
```
Error initializing Firebase: [various errors]
```

**Check:**

1. **Internet connection** - Firebase requires internet
2. **Firebase project exists** - Check Firebase Console
3. **Database is enabled** - Go to Realtime Database in console
4. **Security rules deployed** - Rules tab should show your rules
5. **Service account key is valid** - Regenerate if old

---

### 8. License Validation Fails

**Error:**
```
License key not found
```

**Solutions:**

1. **Create a license first:**
```bash
python admin_gui.py
# Or
python admin_license_manager.py create --email test@test.com --app-id spec-updater --plan premium --duration 365
```

2. **Check Firebase Console:**
   - Go to Realtime Database
   - Look for `licenses/` node
   - Verify license key exists

3. **Check app_id matches:**
   - License must have correct `app_id` field
   - Client must use same `app_id`

---

### 9. Permission Denied Errors

**Error:**
```
Permission denied / Access forbidden
```

**Solutions:**

1. **Check security rules:**
   - Go to Firebase Console → Realtime Database → Rules
   - Copy rules from `firebase-database-rules.json`
   - Click "Publish"

2. **Enable Anonymous Auth:**
   - Firebase Console → Authentication
   - Sign-in method tab
   - Enable "Anonymous"

---

### 10. Rate Limit Errors

**Error:**
```
Quota exceeded / Too many requests
```

**Solution:**

1. **Wait a few minutes** - Firebase has rate limits
2. **Check your plan** - Free tier has limits
3. **Review usage** - Firebase Console → Usage tab

---

## Quick Diagnostics

### Test Firebase Import

```bash
python test_firebase_import.py
```

Expected output:
```
Testing Firebase imports...
1. Testing firebase_admin...
   ✅ firebase_admin imported successfully
2. Testing pyrebase...
   Note: Pyrebase not available (optional)
```

### Test License Creation

```bash
python admin_license_manager.py create \
  --email test@test.com \
  --app-id spec-updater \
  --plan premium \
  --duration 30
```

Should output a license key like: `ABCDE-12345-FGHIJ-67890`

### Test Security

```bash
python test_security.py
```

Should show: `Tests run: 12, Successes: 12`

---

## Environment Check

Run this to check your setup:

```powershell
# Check Python version
python --version
# Should be 3.8 or higher

# Check venv is activated
where python
# Should point to venv\Scripts\python.exe

# Check packages
pip list | findstr firebase
# Should show firebase-admin

# Check files exist
dir firebase*.json
# Should show firebase_config.json and firebase-admin-key.json (or in parent)

# Check Git ignore
type .gitignore | findstr firebase
# Should show both firebase files are ignored
```

---

## Getting Help

If these solutions don't work:

1. **Check error messages carefully** - They often contain the solution
2. **Review documentation:**
   - FIREBASE_SETUP.md - Complete setup guide
   - README.md - Feature documentation
   - SECURITY.md - Security architecture
3. **Check Firebase Console** for errors
4. **Review test output** from `test_firebase_import.py`

---

## Common Mistake Checklist

Before asking for help, verify:

- [ ] Virtual environment is activated
- [ ] In correct directory (`Renamer/`)
- [ ] `firebase_config.json` exists and is valid
- [ ] `firebase-admin-key.json` exists (in Renamer/ or parent/)
- [ ] Firebase project created
- [ ] Realtime Database enabled
- [ ] Anonymous auth enabled
- [ ] Security rules deployed
- [ ] Internet connection active
- [ ] All dependencies installed (`pip install -r requirements.txt`)

---

## Still Having Issues?

1. Delete `venv` and recreate:
```powershell
Remove-Item -Recurse -Force venv
python -m venv venv
.\venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

2. Try the GUI admin tool first:
```bash
python admin_gui.py
```

3. Check if it's a specific file issue:
```bash
python test_firebase_import.py
```

---

## Success Indicators

Everything is working when:

✅ `python test_firebase_import.py` shows firebase-admin imported  
✅ `python admin_gui.py` opens without errors  
✅ Can create licenses via GUI or CLI  
✅ `python update_spec_header_dates_v2.py` opens the main app  
✅ Can enter and validate a license key  
✅ Security tests pass: `python test_security.py`  

---

**Last Updated:** 2025-10-29
