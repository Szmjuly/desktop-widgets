# Subscription Enforcement Guide

## Overview

The application has a **configurable subscription requirement** that allows you to:

- ✅ **Enforce licenses** - Users MUST have a valid license to use the app
- 🔓 **Make optional** - Users can use the app without a license

This is controlled by a secure configuration file that is NOT committed to git.

---

## Current Behavior (Enforced Mode)

When `require_subscription: true` (default):

1. **App launches** → License dialog appears
2. **User enters key:**
   - ✅ Valid key → App opens normally
   - ❌ Invalid key → Error shown, dialog re-appears
   - ❌ Empty key → Warning shown, dialog re-appears
3. **User clicks Cancel:**
   - ❌ Critical message: "Subscription Required"
   - ❌ **App exits immediately**
4. **No bypass possible** - App cannot be used without valid license

---

## Toggle Subscription Requirement

### Check Current Status

```bash
python toggle_subscription_requirement.py status
```

**Output:**
```
============================================================
SUBSCRIPTION REQUIREMENT STATUS
============================================================
Status: 🔒 ENFORCED

Behavior:
  • Users MUST enter a valid license key
  • Cancel button exits the application
  • Invalid keys show error and re-prompt
  • App cannot be used without valid license

Other Settings:
  • App Name: Spec Header Date Updater
  • App Version: 1.0.0
  • Minimum Plan: free
============================================================
```

### Enable Enforcement (Default)

```bash
python toggle_subscription_requirement.py enable
# Or
python toggle_subscription_requirement.py on
```

**Result:**
- Users MUST have valid license
- No bypass available
- Cancel exits app
- Production mode

### Disable Enforcement (Testing/Development)

```bash
python toggle_subscription_requirement.py disable
# Or
python toggle_subscription_requirement.py off
```

**Result:**
- License dialog still shown
- Users CAN cancel and use app
- Useful for testing/demos
- Not recommended for production

---

## Configuration File

**Location:** `app_config.json`

```json
{
  "require_subscription": true,
  "app_name": "Spec Header Date Updater",
  "app_version": "1.0.0",
  "min_plan": "free",
  "feature_flags": {
    "allow_trial": false,
    "trial_days": 0,
    "offline_grace_period_hours": 24
  }
}
```

### Key Settings

| Setting | Values | Description |
|---------|--------|-------------|
| `require_subscription` | `true`/`false` | Enforce license requirement |
| `min_plan` | `free`, `basic`, `premium`, `business` | Minimum plan required |
| `allow_trial` | `true`/`false` | Allow trial period |
| `trial_days` | Number | Trial duration in days |

---

## Security

### Protected Settings

✅ **`app_config.json` is NOT in git** (in `.gitignore`)  
✅ **Cannot be changed by end users** (requires file access)  
✅ **Toggle tool requires admin access** to the Renamer folder  
✅ **License validation happens server-side** (Firebase)  

### Best Practices

1. **Production:** Always use `require_subscription: true`
2. **Development:** Use `false` for testing, re-enable before release
3. **Secure the toggle tool:** Restrict access to `toggle_subscription_requirement.py`
4. **Monitor usage:** Check Firebase logs for validation attempts

---

## User Experience

### When Enforced (Production)

```
User launches app
  ↓
[License Dialog Appears]
  ↓
┌─────────────────────────┐
│ Enter License Key       │
│ [___________________]   │
│                         │
│ [Purchase] [  OK  ]     │
│           [Cancel]      │
└─────────────────────────┘
  ↓
User clicks Cancel
  ↓
❌ "A valid license is required"
❌ App exits
```

### When Optional (Testing)

```
User launches app
  ↓
[License Dialog Appears]
  ↓
User clicks Cancel
  ↓
✅ App opens anyway
⚠️ Subscription status shows "No active subscription"
```

---

## Deployment Scenarios

### Scenario 1: Paid Product

```bash
# Set to enforced mode
python toggle_subscription_requirement.py enable

# Users MUST purchase license
# No free tier available
```

### Scenario 2: Freemium Model

```bash
# Set to optional mode
python toggle_subscription_requirement.py disable

# Users can use basic features free
# Premium features require license (implement feature checks separately)
```

### Scenario 3: Trial Period

```json
{
  "require_subscription": false,
  "feature_flags": {
    "allow_trial": true,
    "trial_days": 14
  }
}
```

---

## Testing

### Test Enforced Mode

1. Enable requirement:
   ```bash
   python toggle_subscription_requirement.py enable
   ```

2. Run app WITHOUT creating license:
   ```bash
   python update_spec_header_dates_v2.py
   ```

3. **Expected:**
   - License dialog appears
   - Cannot cancel
   - Invalid keys rejected
   - Must exit app if no license

4. Create test license:
   ```bash
   python admin_gui.py
   # Create license with your email
   ```

5. Run app and enter test license
   - **Expected:** App opens successfully

### Test Optional Mode

1. Disable requirement:
   ```bash
   python toggle_subscription_requirement.py disable
   ```

2. Run app and click Cancel
   - **Expected:** App opens with warning

---

## Troubleshooting

### Users Can't Access App

**Problem:** License required but users don't have keys

**Solutions:**
1. Create licenses: `python admin_gui.py`
2. Temporarily disable: `python toggle_subscription_requirement.py disable`
3. Check Firebase connectivity

### Toggle Not Working

**Problem:** Changes to config not taking effect

**Solutions:**
1. Restart the application
2. Check `app_config.json` exists and is valid JSON
3. Verify permissions on config file

### Want Different Behavior

**Problem:** Need custom enforcement logic

**Solutions:**
1. Edit `update_spec_header_dates_v2.py`
2. Modify `check_subscription()` method
3. Add custom feature flags to `app_config.json`

---

## Advanced Configuration

### Per-Feature Licensing

You can check subscription in specific features:

```python
def startRun(self):
    # Check if subscription is active
    if self.app_config.get('require_subscription') and not self.subscription_mgr.is_subscribed():
        QMessageBox.warning(
            self,
            "Subscription Required",
            "This feature requires an active subscription."
        )
        return
    
    # Continue with normal operation
    ...
```

### Plan-Based Features

```python
def premium_feature(self):
    info = self.subscription_mgr.get_subscription_info()
    
    if info['plan'] not in ['premium', 'business']:
        QMessageBox.warning(
            self,
            "Premium Feature",
            "This feature requires a Premium or Business plan."
        )
        return
    
    # Premium feature code
    ...
```

---

## Summary

| Mode | Command | User Can Cancel? | Use Case |
|------|---------|------------------|----------|
| **Enforced** | `toggle... enable` | ❌ No | Production |
| **Optional** | `toggle... disable` | ✅ Yes | Testing/Demo |

**Default:** Enforced (secure by default)

**Recommendation:** Keep enforced for production, only disable for internal testing.

---

## Quick Reference

```bash
# Check status
python toggle_subscription_requirement.py status

# Enforce (production)
python toggle_subscription_requirement.py enable

# Optional (testing)
python toggle_subscription_requirement.py disable

# Create license
python admin_gui.py
```

---

**Security Note:** The toggle tool should only be accessible to administrators. Do not distribute it with the end-user application.
