#!/usr/bin/env python3
"""
Spec Header Date Updater - Main Launcher Menu

Choose what to run:
1. Main Application
2. Admin GUI
3. Toggle Subscription Requirement
4. Run Security Tests
"""

import sys
import subprocess
import json
from pathlib import Path
from datetime import datetime, timezone


def show_licensing_status():
    """Display current licensing status (for developers)."""
    print("\n" + "="*60)
    print("LICENSING STATUS")
    print("="*60)
    
    # Check build config first
    build_config_file = Path(__file__).parent / 'build_config.json'
    include_licensing = True
    
    if build_config_file.exists():
        try:
            with open(build_config_file, 'r') as f:
                config = json.load(f)
                include_licensing = config.get('include_licensing', True)
        except (json.JSONDecodeError, IOError):
            pass
    
    if not include_licensing:
        print("\n📦 Licensing Status: ❌ REMOVED FROM BUILD")
        print("\nLicensing code is not included in this build.")
        print("The application will run without any license checks.")
        print("\nTo re-enable licensing:")
        print("  python admin/toggle_licensing_build.py include")
        print("\n" + "="*60)
        return
    
    try:
        # Import SubscriptionManager
        sys.path.insert(0, str(Path(__file__).parent))
        from src.subscription import SubscriptionManager
        
        # Initialize subscription manager
        try:
            sub_mgr = SubscriptionManager(app_id="spec-updater")
        except Exception as e:
            print(f"\n❌ Error initializing subscription manager: {e}")
            print("\nThis may indicate:")
            print("  • Firebase config files are missing")
            print("  • Firebase libraries are not installed")
            print("  • Network connectivity issues")
            return
        
        # Get subscription info
        info = sub_mgr.get_subscription_info()
        device_id = sub_mgr.device_id
        
        # Display status
        status = info.get('status', 'inactive')
        if status == 'active':
            status_icon = "✅"
            status_text = "ACTIVE"
        else:
            status_icon = "❌"
            status_text = "INACTIVE"
        
        print(f"\n{status_icon} Subscription Status: {status_text}")
        print(f"\nDevice ID: {device_id[:8]}...{device_id[-8:]}")
        
        if status == 'active':
            expiry_date = info.get('expiry_date')
            if expiry_date:
                try:
                    expiry = datetime.fromisoformat(expiry_date.replace('Z', '+00:00'))
                    now = datetime.now(timezone.utc)
                    days_remaining = (expiry - now).days
                    
                    print(f"\n📅 Expiry Date: {expiry.strftime('%Y-%m-%d %H:%M:%S UTC')}")
                    
                    if days_remaining > 0:
                        print(f"⏰ Days Remaining: {days_remaining}")
                    else:
                        print("⚠️  License has EXPIRED")
                except (ValueError, TypeError) as e:
                    print(f"\n📅 Expiry Date: {expiry_date}")
            
            plan = info.get('plan', 'unknown')
            print(f"\n💳 Plan: {plan.upper()}")
            
            docs_limit = info.get('documents_limit', 0)
            docs_remaining = info.get('documents_remaining', 0)
            
            if docs_limit < 0:
                print("\n📄 Documents: Unlimited")
            else:
                print(f"\n📄 Documents: {docs_remaining} / {docs_limit} remaining")
                
            # Try to get last validation time
            try:
                sub_file = sub_mgr.subscription_file
                if sub_file.exists():
                    with open(sub_file, 'r') as f:
                        sub_data = json.load(f)
                    last_validated = sub_data.get('last_validated')
                    if last_validated:
                        try:
                            validated_time = datetime.fromisoformat(last_validated.replace('Z', '+00:00'))
                            print(f"\n🔄 Last Validated: {validated_time.strftime('%Y-%m-%d %H:%M:%S UTC')}")
                        except (ValueError, TypeError):
                            print(f"\n🔄 Last Validated: {last_validated}")
            except (IOError, json.JSONDecodeError):
                pass
        else:
            print("\n⚠️  No active subscription found.")
            print("\nTo activate:")
            print("  • Run the Main Application and enter a license key")
            print("  • Use Admin GUI to create/manage licenses")
        
    except ImportError as e:
        print(f"\n❌ Error importing subscription module: {e}")
        print("\nMake sure you're running from the correct directory")
        print("and that all dependencies are installed.")
    except Exception as e:
        print(f"\n❌ Unexpected error: {e}")
        import traceback
        traceback.print_exc()
    
    print("\n" + "="*60)


def clear_license_cache():
    """Delete the local license cache file so a fresh license is created on next launch."""
    import os
    
    print("\n" + "="*60)
    print("CLEAR LICENSE CACHE")
    print("="*60)
    
    if os.name == 'nt':
        cache_dir = Path(os.getenv('LOCALAPPDATA', '~')).expanduser() / 'SpecHeaderUpdater'
    else:
        cache_dir = Path('~').expanduser() / '.config' / 'specheadupdater'
    
    cache_file = cache_dir / 'subscription_spec-updater.json'
    
    if cache_file.exists():
        try:
            with open(cache_file, 'r') as f:
                data = json.load(f)
            print(f"\nCurrent cached license:")
            print(f"  Key:  {data.get('license_key', 'N/A')}")
            print(f"  Plan: {data.get('plan', 'N/A')}")
            print(f"  Last Validated: {data.get('last_validated', 'N/A')}")
        except Exception:
            print("\nCached license file exists but could not be read.")
        
        confirm = input("\nDelete local license cache? (y/n): ").strip().lower()
        if confirm == 'y':
            try:
                cache_file.unlink()
                print("\n✅ License cache deleted successfully.")
                print("A new free license will be created on next app launch.")
            except Exception as e:
                print(f"\n❌ Error deleting cache: {e}")
        else:
            print("\nCancelled — cache was not deleted.")
    else:
        print("\nNo license cache file found.")
        print(f"Expected at: {cache_file}")
    
    print("\n" + "="*60)


def show_menu():
    """Display main menu."""
    print("\n" + "="*60)
    print("SPEC HEADER DATE UPDATER - LAUNCHER")
    print("="*60)
    print("\n1.  Run Main Application")
    print("2.  Run Admin GUI (License Management)")
    print("3.  Toggle Subscription Requirement")
    print("4.  Run Security Tests")
    print("5.  Check Firebase Import")
    print("6.  Show Licensing Status (Dev)")
    print("7.  Toggle Licensing Build (Remove/Include)")
    print("8.  Build Application (PyInstaller)")
    print("9.  Clear License Cache")
    print("10. Exit")
    print("\n" + "="*60)
    
    choice = input("\nEnter your choice (1-10): ").strip()
    return choice


def main():
    """Main launcher loop."""
    while True:
        choice = show_menu()
        
        if choice == '1':
            print("\n🚀 Launching Main Application...")
            subprocess.run([sys.executable, "run_app.py"])
        
        elif choice == '2':
            print("\n🔑 Launching Admin GUI...")
            subprocess.run([sys.executable, "run_admin_gui.py"])
        
        elif choice == '3':
            print("\n⚙️  Launching Subscription Toggle...")
            subprocess.run([sys.executable, "admin/toggle_subscription.py", "status"])
        
        elif choice == '4':
            print("\n🧪 Running Security Tests...")
            subprocess.run([sys.executable, "tests/test_security.py"])
        
        elif choice == '5':
            print("\n🔍 Checking Firebase Import...")
            subprocess.run([sys.executable, "tests/test_firebase_import.py"])
        
        elif choice == '6':
            show_licensing_status()
        
        elif choice == '7':
            print("\n🔧 Toggling Licensing Build...")
            subprocess.run([sys.executable, "admin/toggle_licensing_build.py", "status"])
            print("\nTo change:")
            print("  • Remove: python admin/toggle_licensing_build.py remove")
            print("  • Include: python admin/toggle_licensing_build.py include")
        
        elif choice == '8':
            print("\n📦 Building application executable...")
            subprocess.run([sys.executable, "run_app.py", "--build"])
        
        elif choice == '9':
            clear_license_cache()
        
        elif choice == '10':
            print("\n👋 Goodbye!")
            break
        
        else:
            print("\n❌ Invalid choice. Please enter 1-10.")
        
        input("\nPress Enter to continue...")


if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n👋 Goodbye!")
        sys.exit(0)
