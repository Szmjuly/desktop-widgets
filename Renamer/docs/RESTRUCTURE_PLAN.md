# Project Restructure Plan

## Current Issues
- Too many files in root directory
- Documentation scattered
- Scripts mixed with main code
- No clear separation of concerns

## New Structure

```
Renamer/
├── README.md                          # Main documentation
├── requirements.txt                    # Python dependencies
├── .gitignore                         # Git ignore patterns
├── LICENSE                            # License file
│
├── src/                               # Source code
│   ├── __init__.py
│   ├── main.py                        # Main app (update_spec_header_dates_v2.py)
│   ├── subscription.py                # Subscription management
│   └── workers.py                     # Background workers
│
├── admin/                             # Admin tools
│   ├── __init__.py
│   ├── admin_gui.py                   # GUI admin tool
│   ├── admin_license_manager.py       # CLI admin tool
│   └── toggle_subscription.py         # Subscription toggle
│
├── tests/                             # All tests
│   ├── __init__.py
│   ├── test_security.py               # Security test suite
│   ├── test_firebase_import.py        # Import diagnostics
│   └── test_integration.py            # Integration tests
│
├── config/                            # Configuration
│   ├── firebase_config.example.json   # Firebase config template
│   ├── app_config.example.json        # App config template
│   └── firebase-database-rules.json   # Security rules
│
├── docs/                              # Documentation
│   ├── FIREBASE_SETUP.md
│   ├── QUICKSTART.md
│   ├── API_GUIDE.md
│   ├── MULTI_APP_GUIDE.md
│   ├── SECURITY.md
│   ├── TESTING.md
│   ├── SUBSCRIPTION_ENFORCEMENT.md
│   ├── ADMIN_TOOLS_SUMMARY.md
│   ├── TROUBLESHOOTING.md
│   └── TEST_QUICK_REFERENCE.md
│
├── scripts/                           # Utility scripts
│   ├── setup.py                       # Setup wizard
│   └── dev-env.ps1                    # Development environment
│
└── data/                              # Runtime data (gitignored)
    ├── .gitkeep
    └── README.md                      # Explanation of data folder
```

## Migration Steps

1. Create new directory structure
2. Move files to appropriate locations
3. Update import paths
4. Update documentation references
5. Test all functionality
6. Update .gitignore

## Benefits

✅ **Clear organization** - Easy to find files
✅ **Professional structure** - Industry standard
✅ **Better separation** - Source, admin, tests, docs
✅ **Easier maintenance** - Logical grouping
✅ **Scalable** - Room to grow
