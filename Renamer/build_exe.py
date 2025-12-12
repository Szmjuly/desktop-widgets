#!/usr/bin/env python3
"""
Build script to create a single executable for the Spec Header Date Updater.
Uses PyInstaller to bundle everything into one file.

Usage:
    python build_exe.py

Requirements:
    pip install pyinstaller
"""

import subprocess
import sys
import shutil
from pathlib import Path
from typing import Optional

# Project paths
PROJECT_ROOT = Path(__file__).parent
SRC_DIR = PROJECT_ROOT / "src"
DIST_DIR = PROJECT_ROOT / "dist"
BUILD_DIR = PROJECT_ROOT / "build"

# App info
APP_NAME = "SpecHeaderUpdater"
MAIN_SCRIPT = SRC_DIR / "main_v2.py"  # Use the V2 UI as the entry point
ICON_PATH = PROJECT_ROOT / "icon.ico"
LOGO_PNG_PATH = PROJECT_ROOT / "assets" / "Logo.png"


def resolve_icon_path() -> Optional[Path]:
    if LOGO_PNG_PATH.exists():
        BUILD_DIR.mkdir(parents=True, exist_ok=True)
        ico_path = BUILD_DIR / "app.ico"

        try:
            from PIL import Image

            img = Image.open(LOGO_PNG_PATH)
            img.save(
                ico_path,
                format="ICO",
                sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)],
            )
            return ico_path
        except Exception:
            pass

        try:
            from PySide6.QtGui import QGuiApplication, QImage

            app = QGuiApplication.instance()
            if app is None:
                app = QGuiApplication([])
            image = QImage(str(LOGO_PNG_PATH))
            if not image.isNull() and image.save(str(ico_path), "ICO"):
                return ico_path
        except Exception:
            pass

        return None

    if ICON_PATH.exists():
        return ICON_PATH
    return None


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
    
    # Data files to include
    datas = []
    
    # NOTE: firebase_config.json is NO LONGER needed - config is embedded in code
    # NOTE: firebase-admin-key.json is NEVER bundled - that's admin-only
    app_config = PROJECT_ROOT / "app_config.json"
    
    if app_config.exists():
        datas.append((str(app_config), "."))

    assets_dir = PROJECT_ROOT / "assets"
    if assets_dir.exists():
        datas.append((str(assets_dir), "assets"))
    
    # Build the --add-data arguments
    data_args = []
    for src, dest in datas:
        data_args.extend(["--add-data", f"{src};{dest}"])
    
    # Hidden imports that PyInstaller might miss
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
        "google.cloud",
        "docx",
        "docx.shared",
        "psutil",
        "win32com",
        "win32com.client",
        "pythoncom",
        "json",
        "uuid",
        "secrets",
        "hashlib",
        "src.firebase_config_embedded",  # Embedded Firebase config
    ]
    
    hidden_import_args = []
    for imp in hidden_imports:
        hidden_import_args.extend(["--hidden-import", imp])
    
    # PyInstaller command
    cmd = [
        sys.executable, "-m", "PyInstaller",
        "--name", APP_NAME,
        "--onefile",  # Single executable
        "--windowed",  # No console window (GUI app)
        "--clean",  # Clean build
        "--noconfirm",  # Overwrite output without asking
    ]
    
    # Add icon if it exists
    resolved_icon = resolve_icon_path()
    if resolved_icon is not None and resolved_icon.exists():
        cmd.extend(["--icon", str(resolved_icon)])
    
    # Add data files
    cmd.extend(data_args)
    
    # Add hidden imports
    cmd.extend(hidden_import_args)
    
    # Add the main script
    cmd.append(str(MAIN_SCRIPT))
    
    print("Building executable...")
    print(f"Command: {' '.join(cmd)}")
    print()
    
    # Run PyInstaller
    result = subprocess.run(cmd, cwd=str(PROJECT_ROOT))
    
    if result.returncode == 0:
        exe_path = DIST_DIR / f"{APP_NAME}.exe"
        print()
        print("=" * 60)
        print("BUILD SUCCESSFUL!")
        print("=" * 60)
        print(f"Executable: {exe_path}")
        print(f"Size: {exe_path.stat().st_size / (1024*1024):.1f} MB")
        print()
        print("✅ Firebase config is EMBEDDED - no external config file needed!")
        print()
        print("IMPORTANT: Do NOT include firebase-admin-key.json with the app")
        print("           (that's for admin use only)")
        print()
        return True
    else:
        print("BUILD FAILED!")
        return False


def clean_build():
    """Clean build artifacts."""
    print("Cleaning build artifacts...")
    for folder in [BUILD_DIR, DIST_DIR]:
        if folder.exists():
            shutil.rmtree(folder)
    
    # Remove spec file
    spec_file = PROJECT_ROOT / f"{APP_NAME}.spec"
    if spec_file.exists():
        spec_file.unlink()
    
    print("Clean complete.")


def main():
    """Main build function."""
    import argparse
    
    parser = argparse.ArgumentParser(description="Build Spec Header Updater executable")
    parser.add_argument("--clean", action="store_true", help="Clean build artifacts only")
    args = parser.parse_args()
    
    if args.clean:
        clean_build()
        return
    
    print("=" * 60)
    print("Spec Header Date Updater - Build Script")
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
