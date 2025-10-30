#!/usr/bin/env python3
"""
Copy deeply nested folders that exceed Windows MAX_PATH (260 chars) limitation.
Uses \\?\ prefix to bypass path length restrictions on Windows.
"""

import os
import shutil
import argparse
from pathlib import Path


def enable_long_paths(path):
    """Convert path to Windows long path format if on Windows."""
    if os.name == 'nt':  # Windows
        # Convert to absolute path
        abs_path = os.path.abspath(path)
        # Add \\?\ prefix if not already present
        if not abs_path.startswith('\\\\?\\'):
            if abs_path.startswith('\\\\'):  # UNC path
                return '\\\\?\\UNC\\' + abs_path[2:]
            else:
                return '\\\\?\\' + abs_path
    return path


def copy_with_long_paths(source, dest):
    """
    Copy directory tree with support for long paths on Windows.
    Creates the source folder inside the destination.
    
    Args:
        source: Source directory path
        dest: Destination base directory (source folder will be created inside)
    """
    # Get the source folder name
    source_folder_name = os.path.basename(os.path.normpath(source))
    
    # Create final destination path (dest + source folder name)
    final_dest = os.path.join(dest, source_folder_name)
    
    # Enable long path support
    source_long = enable_long_paths(source)
    dest_long = enable_long_paths(final_dest)
    
    print(f"Copying from: {source}")
    print(f"Copying to: {final_dest}")
    print(f"Using long path format for Windows...")
    
    try:
        # Create destination if it doesn't exist
        os.makedirs(dest_long, exist_ok=True)
        
        # Walk through source directory
        copied_files = 0
        copied_dirs = 0
        
        for root, dirs, files in os.walk(source_long):
            # Calculate relative path
            rel_path = os.path.relpath(root, source_long)
            
            # Create corresponding directory in destination
            if rel_path != '.':
                dest_dir = os.path.join(dest_long, rel_path)
            else:
                dest_dir = dest_long
            
            # Create directories
            for dir_name in dirs:
                src_dir = os.path.join(root, dir_name)
                dst_dir = os.path.join(dest_dir, dir_name)
                try:
                    os.makedirs(dst_dir, exist_ok=True)
                    copied_dirs += 1
                except Exception as e:
                    print(f"Error creating directory {dir_name}: {e}")
            
            # Copy files
            for file_name in files:
                src_file = os.path.join(root, file_name)
                dst_file = os.path.join(dest_dir, file_name)
                try:
                    shutil.copy2(src_file, dst_file)
                    copied_files += 1
                    if copied_files % 100 == 0:
                        print(f"Copied {copied_files} files, {copied_dirs} directories...")
                except Exception as e:
                    print(f"Error copying {file_name}: {e}")
        
        print(f"\n✓ Copy complete!")
        print(f"  Total files copied: {copied_files}")
        print(f"  Total directories created: {copied_dirs}")
        
    except Exception as e:
        print(f"Error during copy operation: {e}")
        raise


def main():
    parser = argparse.ArgumentParser(
        description='Copy deeply nested folders with long path support for Windows',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python copy_deep_folders.py --source C:\\my\\deep\\folder --dest D:\\backup
    (Creates D:\\backup\\folder with all contents)
  
  python copy_deep_folders.py --source "C:\\path with spaces" --dest "D:\\new location"
    (Creates D:\\new location\\spaces with all contents)
        """
    )
    
    parser.add_argument(
        '--source',
        required=True,
        help='Source directory to copy from'
    )
    
    parser.add_argument(
        '--dest',
        required=True,
        help='Destination directory to copy to'
    )
    
    args = parser.parse_args()
    
    # Validate source exists
    if not os.path.exists(args.source):
        print(f"Error: Source directory does not exist: {args.source}")
        return 1
    
    if not os.path.isdir(args.source):
        print(f"Error: Source is not a directory: {args.source}")
        return 1
    
    # Calculate final destination path for display
    source_folder_name = os.path.basename(os.path.normpath(args.source))
    final_dest_display = os.path.join(args.dest, source_folder_name)
    
    # Confirm operation
    print(f"\n{'='*60}")
    print(f"COPY OPERATION")
    print(f"{'='*60}")
    print(f"Source:      {args.source}")
    print(f"Destination: {final_dest_display}")
    print(f"{'='*60}")
    
    response = input("\nProceed with copy? (yes/no): ").strip().lower()
    if response not in ['yes', 'y']:
        print("Operation cancelled.")
        return 0
    
    print()
    copy_with_long_paths(args.source, args.dest)
    
    return 0


if __name__ == '__main__':
    exit(main())
