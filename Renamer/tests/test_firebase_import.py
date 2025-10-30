#!/usr/bin/env python3
"""Test Firebase imports to diagnose the issue."""

print("Testing Firebase imports...")
print("-" * 60)

# Test 1: firebase_admin
print("\n1. Testing firebase_admin...")
try:
    import firebase_admin
    print("   ✅ firebase_admin imported successfully")
    print(f"   Version: {firebase_admin.__version__}")
except Exception as e:
    print(f"   ❌ Failed: {type(e).__name__}: {e}")

# Test 2: firebase_admin submodules
print("\n2. Testing firebase_admin submodules...")
try:
    from firebase_admin import credentials, firestore, auth
    print("   ✅ credentials imported")
    print("   ✅ firestore imported")
    print("   ✅ auth imported")
except Exception as e:
    print(f"   ❌ Failed: {type(e).__name__}: {e}")

# Test 3: pyrebase
print("\n3. Testing pyrebase...")
try:
    import pyrebase
    print("   ✅ pyrebase imported successfully")
    print(f"   Version: {pyrebase.__version__ if hasattr(pyrebase, '__version__') else 'unknown'}")
except Exception as e:
    print(f"   ❌ Failed: {type(e).__name__}: {e}")
    import traceback
    print("\n   Full traceback:")
    traceback.print_exc()

print("\n" + "-" * 60)
print("Import test complete!")
