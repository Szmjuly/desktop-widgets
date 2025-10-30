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
from pathlib import Path


def show_menu():
    """Display main menu."""
    print("\n" + "="*60)
    print("SPEC HEADER DATE UPDATER - LAUNCHER")
    print("="*60)
    print("\n1. Run Main Application")
    print("2. Run Admin GUI (License Management)")
    print("3. Toggle Subscription Requirement")
    print("4. Run Security Tests")
    print("5. Check Firebase Import")
    print("6. Exit")
    print("\n" + "="*60)
    
    choice = input("\nEnter your choice (1-6): ").strip()
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
            print("\n👋 Goodbye!")
            break
        
        else:
            print("\n❌ Invalid choice. Please enter 1-6.")
        
        input("\nPress Enter to continue...")


if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        print("\n\n👋 Goodbye!")
        sys.exit(0)
