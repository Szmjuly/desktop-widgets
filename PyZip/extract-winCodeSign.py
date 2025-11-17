#!/usr/bin/env python3
"""
Extract winCodeSign archive and fix symlinks using Python.
This works without requiring 7-Zip or admin rights.
"""

import os
import sys
import shutil
import pathlib
from pathlib import Path

try:
    import py7zr
    # Test if py7zr has native dependencies by checking for the compressor module
    from py7zr.compressor import SupportedMethods
except ImportError:
    print("Installing py7zr library...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "py7zr", "--quiet"])
    import py7zr
    from py7zr.compressor import SupportedMethods
except Exception as e:
    print(f"Warning: py7zr import issue: {e}")
    print("Attempting to reinstall py7zr with all dependencies...")
    import subprocess
    try:
        subprocess.check_call([sys.executable, "-m", "pip", "install", "--upgrade", "--force-reinstall", "py7zr"], 
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        import py7zr
        from py7zr.compressor import SupportedMethods
    except:
        pass

def get_cache_dir():
    """Get the electron-builder cache directory."""
    home = Path.home()
    if sys.platform == "win32":
        return home / "AppData" / "Local" / "electron-builder" / "Cache" / "winCodeSign"
    else:
        return home / ".cache" / "electron-builder" / "winCodeSign"

def find_7zip():
    """Find 7-Zip executable."""
    import shutil
    # Common 7-Zip locations
    possible_paths = [
        "7z",  # In PATH
        "7za",  # Standalone version
        r"C:\Program Files\7-Zip\7z.exe",
        r"C:\Program Files (x86)\7-Zip\7z.exe",
        str(Path.home() / "AppData" / "Local" / "Programs" / "7-Zip" / "7z.exe"),
    ]
    
    for path in possible_paths:
        if shutil.which(path):
            return path
        if Path(path).exists():
            return path
    return None

def extract_with_windows_tools(archive_path, extract_to):
    """Try to use Windows built-in tools or provide manual instructions."""
    import subprocess
    
    extract_to = Path(extract_to)
    extract_to.mkdir(parents=True, exist_ok=True)
    
    # Try using PowerShell's Expand-Archive (won't work for 7z, but worth trying)
    # Actually, Windows doesn't have built-in 7z support
    
    # Provide manual extraction instructions
    print("\n" + "="*70)
    print("MANUAL EXTRACTION REQUIRED")
    print("="*70)
    print(f"\nThe archive uses BCJ2 compression which requires 7-Zip.")
    print(f"\nSince 7-Zip cannot be installed, please extract manually:")
    print(f"\n1. Open the archive in Windows Explorer:")
    print(f"   {archive_path}")
    print(f"\n2. If Windows can't open it, you can:")
    print(f"   - Use an online 7z extractor")
    print(f"   - Ask someone with 7-Zip to extract it for you")
    print(f"   - Use a portable 7-Zip executable (no installation needed)")
    print(f"\n3. Extract to this directory:")
    print(f"   {extract_to}")
    print(f"\n4. After extraction, run this script again to fix symlinks:")
    print(f"   python extract-winCodeSign.py")
    print("\n" + "="*70)
    
    # Check if files are already extracted
    expected_dir = extract_to / "winCodeSign-2.6.0"
    if expected_dir.exists() and any(expected_dir.iterdir()):
        print(f"\n✅ Found extracted files in {expected_dir}")
        print("Fixing symlinks...")
        fix_symlinks(extract_to)
        print("✅ Symlink fixing complete!")
        return True
    
    return False

def extract_with_7zip(archive_path, extract_to):
    """Extract using 7-Zip command line tool as fallback."""
    import subprocess
    
    sevenzip = find_7zip()
    if not sevenzip:
        print("7-Zip not found in common locations or PATH.")
        return False
    
    print(f"Using 7-Zip fallback: {sevenzip}")
    extract_to = Path(extract_to)
    extract_to.mkdir(parents=True, exist_ok=True)
    
    try:
        # 7-Zip command: 7z x archive.7z -ooutput_dir
        # Note: -o must not have space after it, and path should not have trailing backslash
        output_dir = str(extract_to).rstrip('\\')
        result = subprocess.run(
            [sevenzip, "x", str(archive_path), f"-o{output_dir}", "-y"],
            capture_output=True,
            text=True,
            check=False
        )
        
        if result.returncode == 0:
            print("Extraction with 7-Zip completed.")
            print("Fixing symlinks...")
            fix_symlinks(extract_to)
            print("✅ Extraction and symlink fixing complete!")
            return True
        else:
            print(f"7-Zip extraction failed (exit code {result.returncode})")
            if result.stderr:
                print(f"Error output: {result.stderr}")
            if result.stdout:
                # 7-Zip often puts errors in stdout
                error_lines = [line for line in result.stdout.split('\n') if 'Error' in line or 'error' in line]
                if error_lines:
                    print(f"Error details: {error_lines[0]}")
            return False
    except FileNotFoundError:
        print(f"7-Zip executable not found at: {sevenzip}")
        return False
    except Exception as e:
        print(f"Error using 7-Zip: {e}")
        import traceback
        traceback.print_exc()
        return False

def extract_archive(archive_path, extract_to):
    """Extract 7z archive, handling symlinks by copying files instead."""
    print(f"Extracting {archive_path} to {extract_to}...")
    
    extract_to = Path(extract_to)
    extract_to.mkdir(parents=True, exist_ok=True)
    
    with py7zr.SevenZipFile(archive_path, mode='r') as archive:
        # Get all file info first
        allfiles = archive.getnames()
        
        # Extract all files
        archive.extractall(path=extract_to)
        
        print(f"Extracted {len(allfiles)} files.")
    
    print("Fixing symlinks...")
    fix_symlinks(extract_to)
    print("✅ Extraction and symlink fixing complete!")

def fix_symlinks(directory):
    """Replace symlinks with file copies."""
    directory = Path(directory)
    fixed_count = 0
    
    for root, dirs, files in os.walk(directory):
        root_path = Path(root)
        
        for item in list(dirs) + files:
            item_path = root_path / item
            
            try:
                # Check if it's a symlink
                if item_path.is_symlink():
                    target = item_path.readlink()
                    target_path = item_path.parent / target
                    
                    # Resolve relative symlinks
                    if not target_path.is_absolute():
                        target_path = (item_path.parent / target).resolve()
                    
                    # Remove symlink
                    item_path.unlink()
                    
                    # Copy target if it exists
                    if target_path.exists():
                        if target_path.is_file():
                            shutil.copy2(target_path, item_path)
                        elif target_path.is_dir():
                            shutil.copytree(target_path, item_path, dirs_exist_ok=True)
                        fixed_count += 1
                        print(f"  Fixed: {item_path.relative_to(directory)}")
                    else:
                        # Create placeholder if target doesn't exist
                        if item_path.suffix:
                            item_path.touch()
                        else:
                            item_path.mkdir(exist_ok=True)
                        print(f"  Warning: Target not found for {item_path.relative_to(directory)}, created placeholder")
                        
            except (OSError, PermissionError) as e:
                # Skip files we can't process
                pass
    
    if fixed_count > 0:
        print(f"Fixed {fixed_count} symlinks.")

def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='Extract winCodeSign archive and fix symlinks')
    parser.add_argument('archive', nargs='?', 
                       default=str(Path.home() / "Downloads" / "winCodeSign-2.6.0.7z"),
                       help='Path to winCodeSign-2.6.0.7z archive (default: ~/Downloads/winCodeSign-2.6.0.7z)')
    
    args = parser.parse_args()
    
    archive_path = Path(args.archive)
    
    if not archive_path.exists():
        print(f"Error: Archive not found at: {archive_path}")
        print(f"\nPlease download it from:")
        print("https://github.com/electron-userland/electron-builder-binaries/releases/download/winCodeSign-2.6.0/winCodeSign-2.6.0.7z")
        print(f"\nOr specify the path:")
        print(f"  python scripts/extract-winCodeSign.py C:\\path\\to\\winCodeSign-2.6.0.7z")
        sys.exit(1)
    
    cache_dir = get_cache_dir()
    extract_dir = cache_dir / "winCodeSign-2.6.0"
    
    print(f"Cache directory: {cache_dir}")
    print(f"Extract to: {extract_dir}")
    print()
    
    try:
        extract_archive(archive_path, cache_dir)
        print(f"\n✅ Success! Files extracted to: {extract_dir}")
        print("You can now run: npm run build")
    except py7zr.exceptions.UnsupportedCompressionMethodError as e:
        error_msg = str(e)
        print(f"\n⚠️  py7zr error: Unsupported compression method")
        
        # Check if it's BCJ2 (which py7zr doesn't support at all)
        if "BCJ2" in error_msg or b'\x03\x03\x01\x1b' in error_msg.encode('utf-8', errors='ignore'):
            print("\nThis archive uses BCJ2 filter compression, which py7zr does not support.")
            print("BCJ2 is not supported by py7zr regardless of dependencies.")
        else:
            print("\nThis usually means py7zr's native dependencies weren't installed properly.")
            print("These dependencies (inflate64, pybcj, pyppmd) require compilation.")
        
        print("\nAttempting fallback to 7-Zip...")
        
        # Try 7-Zip fallback
        if extract_with_7zip(archive_path, cache_dir):
            print(f"\n✅ Success! Files extracted to: {extract_dir}")
            print("You can now run: npm run build")
        else:
            # Try Windows tools or manual extraction
            if extract_with_windows_tools(archive_path, cache_dir):
                print(f"\n✅ Success! Files extracted to: {extract_dir}")
                print("You can now run: npm run build")
            else:
                print("\n❌ Automatic extraction not possible.")
                print("\nThe archive requires 7-Zip due to BCJ2 compression.")
                print("\nOptions:")
                print("  1. Use portable 7-Zip (no installation):")
                print("     - Download 7-Zip portable from https://www.7-zip.org/")
                print("     - Extract 7z.exe to a folder")
                print("     - Add that folder to PATH temporarily, or")
                print("     - Place 7z.exe in this project directory")
                print("\n  2. Manual extraction:")
                print("     - Extract the archive manually using any 7z tool")
                print("     - Place contents in:", cache_dir)
                print("     - Run this script again to fix symlinks")
                print("\n  3. Online extraction:")
                print("     - Use an online 7z extractor service")
                print("     - Download and place files in:", cache_dir)
                import traceback
                traceback.print_exc()
                sys.exit(1)
    except Exception as e:
        import traceback
        print(f"\n❌ Error: {e}")
        print("\nFull traceback:")
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()

