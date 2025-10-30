#!/usr/bin/env python3
"""
Admin Tool: Toggle Subscription Requirement

Securely enable or disable the subscription requirement for the application.
This allows you to:
- Enforce licenses (require_subscription: true) - Users MUST have valid license
- Make licenses optional (require_subscription: false) - Users can use without license
"""

import json
import argparse
from pathlib import Path


def load_config():
    """Load current app configuration."""
    config_file = Path(__file__).parent / 'app_config.json'
    
    if not config_file.exists():
        return {
            "require_subscription": True,
            "app_name": "Spec Header Date Updater",
            "app_version": "1.0.0",
            "min_plan": "free"
        }
    
    with open(config_file, 'r') as f:
        return json.load(f)


def save_config(config):
    """Save app configuration."""
    config_file = Path(__file__).parent / 'app_config.json'
    
    with open(config_file, 'w') as f:
        json.dump(config, f, indent=2)
    
    print(f"✅ Configuration saved to: {config_file}")


def show_status():
    """Display current subscription requirement status."""
    config = load_config()
    
    print("\n" + "="*60)
    print("SUBSCRIPTION REQUIREMENT STATUS")
    print("="*60)
    
    require_sub = config.get('require_subscription', True)
    
    if require_sub:
        print("Status: 🔒 ENFORCED")
        print("\nBehavior:")
        print("  • Users MUST enter a valid license key")
        print("  • Cancel button exits the application")
        print("  • Invalid keys show error and re-prompt")
        print("  • App cannot be used without valid license")
    else:
        print("Status: 🔓 OPTIONAL")
        print("\nBehavior:")
        print("  • Users can cancel license dialog")
        print("  • App can be used without license")
        print("  • License entry is available but not required")
    
    print("\nOther Settings:")
    print(f"  • App Name: {config.get('app_name', 'N/A')}")
    print(f"  • App Version: {config.get('app_version', 'N/A')}")
    print(f"  • Minimum Plan: {config.get('min_plan', 'N/A')}")
    
    print("="*60 + "\n")


def enable_requirement():
    """Enable subscription requirement (enforce licenses)."""
    config = load_config()
    config['require_subscription'] = True
    save_config(config)
    
    print("\n🔒 Subscription requirement is now ENFORCED")
    print("   Users MUST have a valid license to use the app.\n")


def disable_requirement():
    """Disable subscription requirement (make optional)."""
    config = load_config()
    config['require_subscription'] = False
    save_config(config)
    
    print("\n🔓 Subscription requirement is now OPTIONAL")
    print("   Users can use the app without a license.\n")


def main():
    parser = argparse.ArgumentParser(
        description='Toggle subscription requirement for the application',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Show current status
  python toggle_subscription_requirement.py status
  
  # Require subscription (enforce licenses)
  python toggle_subscription_requirement.py enable
  
  # Make subscription optional
  python toggle_subscription_requirement.py disable

Security:
  This tool directly modifies app_config.json.
  Keep this file secure and do not commit it to version control.
        """
    )
    
    parser.add_argument(
        'action',
        choices=['status', 'enable', 'disable', 'on', 'off'],
        help='Action to perform'
    )
    
    args = parser.parse_args()
    
    if args.action == 'status':
        show_status()
    elif args.action in ['enable', 'on']:
        enable_requirement()
        show_status()
    elif args.action in ['disable', 'off']:
        disable_requirement()
        show_status()


if __name__ == '__main__':
    main()
