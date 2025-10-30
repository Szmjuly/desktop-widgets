# Project Structure

## Directory Layout

```
Renamer/
├── README.md                          # Main documentation
├── requirements.txt                    # Python dependencies
├── .gitignore                         # Git ignore patterns
├── launcher.py                        # Main launcher menu
├── run_app.py                         # Quick app launcher
├── run_admin_gui.py                   # Quick admin GUI launcher
│
├── src/                               # Source code
│   ├── __init__.py
│   ├── main.py                        # Main application
│   └── subscription.py                # Subscription management
│
├── admin/                             # Admin tools
│   ├── __init__.py
│   ├── admin_gui.py                   # GUI admin tool
│   ├── admin_license_manager.py       # CLI admin tool
│   └── toggle_subscription.py         # Subscription toggle
│
├── tests/                             # Test suite
│   ├── __init__.py
│   ├── test_security.py               # Security tests
│   └── test_firebase_import.py        # Import diagnostics
│
├── config/                            # Configuration templates
│   ├── firebase_config.example.json   # Firebase config template
│   ├── app_config.example.json        # App config template
│   └── firebase-database-rules.json   # Security rules for Firebase
│
├── docs/                              # Documentation
│   ├── FIREBASE_SETUP.md              # Firebase setup guide
│   ├── QUICKSTART.md                  # Quick start guide
│   ├── API_GUIDE.md                   # API integration
│   ├── MULTI_APP_GUIDE.md             # Multi-app management
│   ├── SECURITY.md                    # Security architecture
│   ├── TESTING.md                     # Testing guide
│   ├── SUBSCRIPTION_ENFORCEMENT.md    # Subscription control
│   ├── ADMIN_TOOLS_SUMMARY.md         # Admin tools overview
│   ├── TROUBLESHOOTING.md             # Common issues
│   └── TEST_QUICK_REFERENCE.md        # Test commands
│
├── scripts/                           # Utility scripts
│   └── (future automation scripts)
│
├── data/                              # Runtime data (gitignored)
│   └── README.md                      # Data folder info
│
└── venv/                              # Virtual environment (gitignored)
```

## Quick Start

### Option 1: Using the Launcher Menu

```bash
python launcher.py
```

This shows a menu with all options:
1. Run Main Application
2. Run Admin GUI
3. Toggle Subscription
4. Run Security Tests
5. Check Firebase Import

### Option 2: Direct Launch

```bash
# Run main app
python run_app.py

# Run admin GUI
python run_admin_gui.py

# Run tests
python tests/test_security.py
```

## File Descriptions

### Root Files

| File | Purpose |
|------|---------|
| `launcher.py` | Interactive menu for all tools |
| `run_app.py` | Quick launcher for main app |
| `run_admin_gui.py` | Quick launcher for admin GUI |
| `requirements.txt` | Python package dependencies |
| `.gitignore` | Git ignore patterns |
| `README.md` | Main documentation |

### Source Code (`src/`)

| File | Purpose |
|------|---------|
| `main.py` | Main application (formerly update_spec_header_dates_v2.py) |
| `subscription.py` | Subscription and license validation |

### Admin Tools (`admin/`)

| File | Purpose |
|------|---------|
| `admin_gui.py` | Graphical license management |
| `admin_license_manager.py` | Command-line license management |
| `toggle_subscription.py` | Toggle subscription requirement |

### Tests (`tests/`)

| File | Purpose |
|------|---------|
| `test_security.py` | Comprehensive security test suite |
| `test_firebase_import.py` | Firebase import diagnostics |

### Configuration (`config/`)

| File | Purpose |
|------|---------|
| `firebase_config.example.json` | Template for Firebase config |
| `app_config.example.json` | Template for app settings |
| `firebase-database-rules.json` | Firebase security rules |

**Note:** Actual config files (without `.example`) are gitignored and created locally.

### Documentation (`docs/`)

All markdown documentation files for setup, usage, and reference.

## Configuration Setup

1. **Copy example configs:**
   ```bash
   copy config\firebase_config.example.json firebase_config.json
   copy config\app_config.example.json app_config.json
   ```

2. **Edit with your Firebase details:**
   - `firebase_config.json` - Firebase project settings
   - `app_config.json` - Application settings

3. **Download Firebase admin key:**
   - From Firebase Console → Project Settings → Service Accounts
   - Save as `firebase-admin-key.json` in root directory

## Import Paths

The new structure uses proper Python imports:

```python
# In main app
from src.subscription import SubscriptionManager

# In admin tools
from admin.admin_license_manager import LicenseManager
```

Launcher scripts handle path setup automatically.

## Benefits of New Structure

✅ **Organized** - Clear separation of concerns  
✅ **Professional** - Industry-standard layout  
✅ **Scalable** - Easy to add new features  
✅ **Maintainable** - Files easy to find  
✅ **Clean Root** - Less clutter in main directory  
✅ **Better Git** - Clear what should be ignored  

## Migration from Old Structure

If you have the old structure, files were moved as follows:

| Old Location | New Location |
|--------------|--------------|
| `update_spec_header_dates_v2.py` | `src/main.py` |
| `subscription.py` | `src/subscription.py` |
| `admin_gui.py` | `admin/admin_gui.py` |
| `admin_license_manager.py` | `admin/admin_license_manager.py` |
| `toggle_subscription_requirement.py` | `admin/toggle_subscription.py` |
| `test_*.py` | `tests/test_*.py` |
| `*.md` (except README) | `docs/*.md` |
| `*_config.example.json` | `config/*_config.example.json` |

## Development Workflow

1. **Code** - Edit files in `src/` or `admin/`
2. **Test** - Run tests from `tests/`
3. **Document** - Update docs in `docs/`
4. **Configure** - Manage settings in `config/` examples
5. **Launch** - Use `launcher.py` or direct launchers

## Adding New Features

### New Source File

Add to `src/` directory and import in `src/main.py`

### New Admin Tool

Add to `admin/` directory and add option to `launcher.py`

### New Test

Add to `tests/` directory with `test_` prefix

### New Documentation

Add to `docs/` directory as `.md` file

## Deployment

For deployment:
1. Include: `src/`, `admin/`, `config/*.example.json`, `docs/`, `requirements.txt`
2. Exclude: `tests/`, `data/`, `venv/`, `*.pyc`, `__pycache__/`
3. User creates: `firebase_config.json`, `firebase-admin-key.json`, `app_config.json`

---

**Last Updated:** 2025-10-29
