#!/usr/bin/env python3
"""
Build script to create a single executable for the Admin License Manager.
This is for YOUR use only - never distribute this!

Usage:
    python build_admin.py
"""

import subprocess
import sys
import shutil
from pathlib import Path

# Project paths
PROJECT_ROOT = Path(__file__).parent
ADMIN_DIR = PROJECT_ROOT / "admin"
DIST_DIR = PROJECT_ROOT / "dist"
BUILD_DIR = PROJECT_ROOT / "build"

# App info
APP_NAME = "LicenseManager"
MAIN_SCRIPT = ADMIN_DIR / "admin_gui_v2.py"


def check_pyinstaller():
    """Check if PyInstaller is installed."""
    try:
        import PyInstaller
        return True
    except ImportError:
        return False


def install_pyinstaller():
    """Install PyInstaller."""
    print("Installing PyInstaller...")
    subprocess.check_call([sys.executable, "-m", "pip", "install", "pyinstaller"])


def build_executable():
    """Build the executable using PyInstaller."""
    
    # Data files to include - Admin tool NEEDS the admin key
    datas = []
    
    firebase_config = PROJECT_ROOT / "firebase_config.json"
    firebase_admin_key = PROJECT_ROOT / "firebase-admin-key.json"
    
    if firebase_config.exists():
        datas.append((str(firebase_config), "."))
    if firebase_admin_key.exists():
        datas.append((str(firebase_admin_key), "."))
    
    # Build the --add-data arguments
    data_args = []
    for src, dest in datas:
        data_args.extend(["--add-data", f"{src};{dest}"])
    
    # Hidden imports
    hidden_imports = [
        "PySide6.QtCore",
        "PySide6.QtGui", 
        "PySide6.QtWidgets",
        "firebase_admin",
        "firebase_admin.credentials",
        "firebase_admin.db",
        "firebase_admin.auth",
        "google.auth",
        "google.auth.transport",
        "google.auth.transport.requests",
        "google.oauth2",
        "admin.admin_license_manager",
    ]
    
    hidden_import_args = []
    for imp in hidden_imports:
        hidden_import_args.extend(["--hidden-import", imp])
    
    # PyInstaller command
    cmd = [
        sys.executable, "-m", "PyInstaller",
        "--name", APP_NAME,
        "--onefile",
        "--windowed",
        "--clean",
        "--noconfirm",
    ]
    
    # Add data files
    cmd.extend(data_args)
    
    # Add hidden imports
    cmd.extend(hidden_import_args)
    
    # Add paths for imports
    cmd.extend(["--paths", str(PROJECT_ROOT)])
    
    # Add the main script
    cmd.append(str(MAIN_SCRIPT))
    
    print("Building Admin executable...")
    print(f"Command: {' '.join(cmd)}")
    print()
    
    # Run PyInstaller
    result = subprocess.run(cmd, cwd=str(PROJECT_ROOT))
    
    if result.returncode == 0:
        exe_path = DIST_DIR / f"{APP_NAME}.exe"
        print()
        print("=" * 60)
        print("ADMIN BUILD SUCCESSFUL!")
        print("=" * 60)
        print(f"Executable: {exe_path}")
        print(f"Size: {exe_path.stat().st_size / (1024*1024):.1f} MB")
        print()
        print("⚠️  IMPORTANT: This is an ADMIN-ONLY tool!")
        print("   Do NOT distribute this executable.")
        print("   Keep firebase-admin-key.json secure.")
        print()
        return True
    else:
        print("BUILD FAILED!")
        return False


def main():
    print("=" * 60)
    print("License Manager Admin Tool - Build Script")
    print("=" * 60)
    print()
    
    # Check/install PyInstaller
    if not check_pyinstaller():
        install_pyinstaller()
    
    # Build
    success = build_executable()
    
    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
