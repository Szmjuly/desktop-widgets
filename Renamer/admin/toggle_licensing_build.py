#!/usr/bin/env python3
"""
Build Configuration Manager - Toggle Licensing Inclusion

This script allows you to completely remove licensing code from the build,
rather than just disabling it. This is useful for:
- Company presentations without licensing
- Internal-only builds
- Different distribution versions

Usage:
    python toggle_licensing_build.py status    # Show current state
    python toggle_licensing_build.py include   # Include licensing code
    python toggle_licensing_build.py remove    # Remove licensing code
"""

import json
import argparse
from pathlib import Path


BUILD_CONFIG_FILE = Path(__file__).parent.parent / 'build_config.json'
DEFAULT_CONFIG = {
    "include_licensing": True,
    "build_version": "1.0.0",
    "description": "Controls whether licensing code is included in the build"
}


def load_build_config():
    """Load build configuration, creating default if missing."""
    if BUILD_CONFIG_FILE.exists():
        try:
            with open(BUILD_CONFIG_FILE, 'r') as f:
                config = json.load(f)
                # Ensure include_licensing exists
                if 'include_licensing' not in config:
                    config['include_licensing'] = True
                return config
        except (json.JSONDecodeError, IOError) as e:
            print(f"⚠️  Error reading build config: {e}")
            print("   Creating default configuration...")
    
    # Create default config
    save_build_config(DEFAULT_CONFIG)
    return DEFAULT_CONFIG.copy()


def save_build_config(config):
    """Save build configuration."""
    try:
        BUILD_CONFIG_FILE.parent.mkdir(parents=True, exist_ok=True)
        with open(BUILD_CONFIG_FILE, 'w') as f:
            json.dump(config, f, indent=2)
        return True
    except IOError as e:
        print(f"❌ Error saving build config: {e}")
        return False


def show_status():
    """Display current build configuration status."""
    config = load_build_config()
    
    print("\n" + "="*60)
    print("BUILD CONFIGURATION STATUS")
    print("="*60)
    
    include_licensing = config.get('include_licensing', True)
    
    if include_licensing:
        print("\n📦 Licensing Status: ✅ INCLUDED")
        print("\nCurrent Behavior:")
        print("  • Licensing code is ACTIVE")
        print("  • Subscription checks are enforced")
        print("  • License dialogs will appear")
        print("  • Firebase dependencies are loaded")
        print("\n⚠️  Note: Users can still bypass by modifying code/config")
    else:
        print("\n📦 Licensing Status: ❌ REMOVED")
        print("\nCurrent Behavior:")
        print("  • Licensing code is NOT included")
        print("  • No subscription checks")
        print("  • No license dialogs")
        print("  • No Firebase dependencies")
        print("  • Faster startup, cleaner code")
        print("\n✅ Safe for company presentations")
    
    print(f"\nBuild Version: {config.get('build_version', 'N/A')}")
    print(f"Config File: {BUILD_CONFIG_FILE}")
    print("="*60 + "\n")


def include_licensing():
    """Enable licensing code inclusion."""
    config = load_build_config()
    
    if config.get('include_licensing', True):
        print("\n✅ Licensing is already INCLUDED in the build.")
        print("   No changes needed.\n")
        return
    
    config['include_licensing'] = True
    
    if save_build_config(config):
        print("\n✅ Licensing code is now INCLUDED in the build")
        print("   The next time you run the app, licensing will be active.\n")
        show_status()
    else:
        print("\n❌ Failed to update build configuration\n")


def remove_licensing():
    """Remove licensing code from build."""
    config = load_build_config()
    
    if not config.get('include_licensing', True):
        print("\n✅ Licensing is already REMOVED from the build.")
        print("   No changes needed.\n")
        return
    
    print("\n⚠️  WARNING: You are about to remove licensing code from the build.")
    print("   This will:")
    print("     • Remove all subscription checks")
    print("     • Remove license dialogs")
    print("     • Skip Firebase dependencies")
    print("     • Make the app free to use")
    print("\n   This is SAFE for company presentations.")
    print("   You can always re-enable later.\n")
    
    response = input("Continue? (yes/no): ").strip().lower()
    if response not in ['yes', 'y']:
        print("\n❌ Operation cancelled.\n")
        return
    
    config['include_licensing'] = False
    
    if save_build_config(config):
        print("\n✅ Licensing code is now REMOVED from the build")
        print("   The next time you run the app, no licensing will be required.\n")
        show_status()
    else:
        print("\n❌ Failed to update build configuration\n")


def main():
    parser = argparse.ArgumentParser(
        description='Toggle licensing code inclusion in build',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Show current status
  python toggle_licensing_build.py status
  
  # Include licensing code (default)
  python toggle_licensing_build.py include
  
  # Remove licensing code (for presentations)
  python toggle_licensing_build.py remove

Security Note:
  This is a BUILD-TIME configuration. End users cannot toggle this.
  For production, use PyInstaller or similar to create frozen binaries.
        """
    )
    
    parser.add_argument(
        'action',
        choices=['status', 'include', 'remove'],
        help='Action to perform'
    )
    
    args = parser.parse_args()
    
    if args.action == 'status':
        show_status()
    elif args.action == 'include':
        include_licensing()
    elif args.action == 'remove':
        remove_licensing()


if __name__ == '__main__':
    main()

