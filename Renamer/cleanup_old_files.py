#!/usr/bin/env python3
"""
Cleanup Script - Remove Old Duplicate Files

This script removes old files that have been moved to the new structure.
It will:
1. Delete duplicate Python files (now in src/, admin/, tests/)
2. Move config examples to config/
3. Clean up __pycache__ directories
4. Keep actual config files in root (firebase_config.json, etc.)
"""

import os
import shutil
from pathlib import Path


def safe_delete(file_path: Path, description: str):
    """Safely delete a file with confirmation."""
    if file_path.exists():
        print(f"  🗑️  Deleting: {description}")
        print(f"      {file_path}")
        file_path.unlink()
        return True
    return False


def safe_move(src: Path, dst: Path, description: str):
    """Safely move a file."""
    if src.exists() and src != dst:
        print(f"  📦 Moving: {description}")
        print(f"      From: {src}")
        print(f"      To:   {dst}")
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(src), str(dst))
        return True
    return False


def clean_pycache(root: Path):
    """Remove all __pycache__ directories."""
    print("\n🧹 Cleaning __pycache__ directories...")
    count = 0
    for pycache in root.rglob('__pycache__'):
        if pycache.is_dir():
            print(f"  🗑️  Removing: {pycache}")
            shutil.rmtree(pycache)
            count += 1
    print(f"  ✅ Removed {count} __pycache__ directories")


def main():
    """Main cleanup function."""
    root = Path(__file__).parent
    
    print("="*70)
    print("PROJECT CLEANUP - Remove Old Duplicate Files")
    print("="*70)
    print("\nThis will remove old files that have been moved to the new structure.")
    print("\n⚠️  WARNING: This will DELETE files!")
    print("Make sure you have a backup or can recover from git if needed.")
    
    response = input("\n🤔 Do you want to proceed? (yes/no): ").strip().lower()
    if response not in ['yes', 'y']:
        print("\n❌ Cleanup cancelled.")
        return
    
    print("\n" + "="*70)
    print("Starting cleanup...")
    print("="*70)
    
    # 1. Delete duplicate Python files (now in src/, admin/, tests/)
    print("\n📂 Removing duplicate Python files from root...")
    
    old_files = {
        'admin_gui.py': 'Old admin GUI (now in admin/)',
        'admin_license_manager.py': 'Old license manager (now in admin/)',
        'subscription.py': 'Old subscription module (now in src/)',
        'test_firebase_import.py': 'Old test (now in tests/)',
        'test_security.py': 'Old test (now in tests/)',
        'toggle_subscription_requirement.py': 'Old toggle tool (now in admin/)',
        'update_spec_header_dates_v2.py': 'Old main app (now src/main.py)',
        'update_spec_header_dates.py': 'Old version of main app',
    }
    
    deleted_count = 0
    for filename, description in old_files.items():
        if safe_delete(root / filename, description):
            deleted_count += 1
    
    print(f"\n  ✅ Deleted {deleted_count} duplicate Python files")
    
    # 2. Move config examples to config/
    print("\n📦 Moving config files to config/...")
    
    config_files = {
        'firebase_config.example.json': 'Firebase config example',
        'firebase-database-rules.json': 'Firebase security rules',
    }
    
    moved_count = 0
    for filename, description in config_files.items():
        src = root / filename
        dst = root / 'config' / filename
        if safe_move(src, dst, description):
            moved_count += 1
    
    print(f"\n  ✅ Moved {moved_count} config files")
    
    # 3. Clean up __pycache__
    clean_pycache(root)
    
    # 4. Clean up old setup files if they exist
    print("\n🧹 Removing old setup files...")
    old_setup_files = ['setup.py', 'run']  # 'run' looks like a stray file
    
    for filename in old_setup_files:
        file_path = root / filename
        if file_path.exists():
            if file_path.is_file():
                safe_delete(file_path, f"Old file: {filename}")
            elif file_path.is_dir():
                print(f"  🗑️  Removing directory: {file_path}")
                shutil.rmtree(file_path)
    
    # 5. Summary of what should remain
    print("\n" + "="*70)
    print("✅ CLEANUP COMPLETE!")
    print("="*70)
    
    print("\n📁 Your root directory should now have:")
    print("""
    Renamer/
    ├── 📁 admin/                    # Admin tools
    ├── 📁 config/                   # Config templates
    ├── 📁 data/                     # Runtime data
    ├── 📁 docs/                     # Documentation
    ├── 📁 scripts/                  # Utility scripts
    ├── 📁 src/                      # Source code
    ├── 📁 tests/                    # Tests
    ├── 📁 venv/                     # Virtual environment
    │
    ├── .env                         # Environment variables
    ├── .gitignore                   # Git ignore
    ├── launcher.py                  # Main launcher
    ├── run_app.py                   # App launcher
    ├── run_admin_gui.py             # Admin launcher
    ├── requirements.txt             # Dependencies
    │
    ├── app_config.json              # App config (actual)
    ├── firebase_config.json         # Firebase config (actual)
    ├── firebase-admin-key.json      # Admin key (actual)
    │
    ├── PROJECT_STRUCTURE.md         # Structure docs
    ├── README.md                    # Main docs
    └── REORGANIZATION_COMPLETE.md   # Reorganization info
    """)
    
    print("\n✅ The root is now clean and organized!")
    print("✅ All duplicates removed")
    print("✅ Config examples in config/")
    print("✅ Source code in proper directories")
    
    print("\n🚀 Test your setup:")
    print("   python launcher.py")
    
    print("\n" + "="*70)


if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n❌ Cleanup cancelled by user")
    except Exception as e:
        print(f"\n\n❌ Error during cleanup: {e}")
        print("You may need to clean up manually.")
