# Licensing Toggle System - Developer Guide

## Overview

This system allows you to **completely remove** licensing code from the application at build time, rather than just disabling it. This is useful when:
- Presenting to your company without licensing
- Building internal-only versions
- Creating different builds for different purposes

---

## How It Works

### 1. Feature Flag System

A build configuration file (`build_config.json`) controls whether licensing code is included:

```json
{
  "include_licensing": false,
  "build_version": "1.0.0"
}
```

### 2. Conditional Code Loading

The application checks the build config and:
- **If `include_licensing: false`**: Licensing code is completely skipped
- **If `include_licensing: true`**: Full licensing system active

### 3. Build Script

A Python script (`build_with_licensing.py`) handles:
- Setting the feature flag
- Creating builds with/without licensing
- Validating the build configuration

---

## Usage

### Quick Toggle (Via Launcher)

```bash
# Remove licensing entirely
python launcher.py
# Select option: "Toggle Licensing Build (Remove/Include)"

# Or use command line
python admin/toggle_licensing_build.py remove
python admin/toggle_licensing_build.py include
```

### Manual Toggle

```bash
# Edit build_config.json
{
  "include_licensing": false  # Set to false to remove
}

# Then run your app normally
python run_app.py
```

---

## What Gets Removed

When `include_licensing: false`:

1. ✅ **No subscription checks** - App starts immediately
2. ✅ **No license dialogs** - No prompts shown
3. ✅ **No Firebase dependencies** - Faster startup
4. ✅ **No subscription UI elements** - Cleaner interface
5. ✅ **No license validation code** - Smaller codebase

---

## Safety Features

### Security Checks

1. **Build config validation** - Ensures valid configuration
2. **Clean removal** - No broken imports or references
3. **Restore capability** - Can re-enable anytime

### Developer Warnings

- Warning if trying to use admin tools without licensing
- Clear error messages if licensing required but disabled
- Build verification before distribution

---

## Implementation Details

### Modified Files

1. **`src/main.py`**
   - Conditional imports
   - Conditional subscription checks
   - Conditional UI elements

2. **`admin/toggle_licensing_build.py`**
   - New script to toggle build config
   - Validation and safety checks

3. **`build_config.json`**
   - Build-time configuration file

4. **`launcher.py`**
   - New menu option for toggling

---

## Comparison: Toggle vs Remove

| Feature | `toggle_subscription.py` | `toggle_licensing_build.py` |
|---------|------------------------|----------------------------|
| **What it does** | Enables/disables enforcement | Removes/adds code entirely |
| **Code still present** | ✅ Yes | ❌ No |
| **Firebase deps** | ✅ Loaded | ❌ Not imported |
| **License dialog** | ✅ Shown (can cancel) | ❌ Never shown |
| **Use case** | Runtime toggle | Build-time removal |
| **Security** | Config file editable | Build-time only |

---

## Examples

### Scenario 1: Company Presentation

```bash
# 1. Remove licensing
python admin/toggle_licensing_build.py remove

# 2. Verify it's removed
python launcher.py
# Select: "Show Licensing Status (Dev)"
# Should show: "Licensing not included in this build"

# 3. Run app - no license prompts!
python run_app.py
```

### Scenario 2: Re-enable Licensing

```bash
# 1. Include licensing again
python admin/toggle_licensing_build.py include

# 2. Verify
python admin/toggle_licensing_build.py status

# 3. Run app - licensing active
python run_app.py
```

---

## Safety Considerations

### ✅ Safe Because:

1. **Build-time only** - End users can't toggle it
2. **Source control** - Config file can be gitignored
3. **Validation** - Script validates before making changes
4. **Reversible** - Can always re-enable

### ⚠️ Important Notes:

1. **Don't commit `build_config.json`** if you want different builds
2. **Distribute separate builds** for licensed/unlicensed versions
3. **Test thoroughly** after toggling

---

## Next Steps

1. Review the implementation
2. Test the toggle system
3. Create separate builds for presentation vs production
4. Document your company's build process

