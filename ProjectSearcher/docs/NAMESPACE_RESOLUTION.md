# Namespace Resolution - WPF + WinForms Hybrid

## Problem
When both `UseWPF` and `UseWindowsForms` are enabled, several types have the same name in both frameworks, causing ambiguity errors.

## Ambiguous Types

| Type | WPF Namespace | WinForms Namespace |
|------|---------------|-------------------|
| `Application` | `System.Windows.Application` | `System.Windows.Forms.Application` |
| `KeyEventArgs` | `System.Windows.Input.KeyEventArgs` | `System.Windows.Forms.KeyEventArgs` |
| `MouseButtonEventArgs` | `System.Windows.Input.MouseButtonEventArgs` | `System.Windows.Forms.MouseEventArgs` |
| `MessageBox` | `System.Windows.MessageBox` | `System.Windows.Forms.MessageBox` |

## Solution: Fully Qualified Names

### App.xaml.cs
```csharp
// Base class
public partial class App : System.Windows.Application

// MessageBox
System.Windows.MessageBox.Show(...,
    System.Windows.MessageBoxButton.OK,
    System.Windows.MessageBoxImage.Error);
```

### SearchOverlay.xaml.cs
```csharp
// Event handlers
private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
private void ResultsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)

// Key enum
case System.Windows.Input.Key.Escape:
case System.Windows.Input.Key.Enter:

// Keyboard modifiers
System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control
```

### TrayIcon.cs
```csharp
// WinForms types
private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
var contextMenu = new System.Windows.Forms.ContextMenuStrip();
System.Windows.Forms.ToolTipIcon.Info

// WPF types
System.Windows.MessageBox.Show(...);
System.Windows.Application.Current.Shutdown();
```

## Files Modified

1. **src/ProjectSearcher.UI/ProjectSearcher.UI.csproj**
   - Enabled both `UseWPF` and `UseWindowsForms`

2. **src/ProjectSearcher.UI/App.xaml.cs**
   - Qualified `Application` base class
   - Qualified `MessageBox` and related enums

3. **src/ProjectSearcher.UI/SearchOverlay.xaml.cs**
   - Qualified `KeyEventArgs` parameter
   - Qualified `MouseButtonEventArgs` parameter
   - Qualified `Key` enum cases
   - Qualified `Keyboard` and `ModifierKeys`

4. **src/ProjectSearcher.UI/TrayIcon.cs**
   - Qualified all WinForms types
   - Qualified all WPF types

## Why Not Use `using` Aliases?

We could use aliases like:
```csharp
using WpfApp = System.Windows.Application;
using WinFormsNotify = System.Windows.Forms.NotifyIcon;
```

However, fully qualified names are preferred because:
- ✅ More explicit and clear
- ✅ No risk of alias conflicts
- ✅ Easier to understand which framework is being used
- ✅ Better for code reviews and maintenance

## Build Verification

After these changes, the build should succeed:

```powershell
.\run
```

Expected output:
```
✅ Restoring packages...
✅ Building ProjectSearcher.Core...
✅ Building ProjectSearcher.Infrastructure...
✅ Building ProjectSearcher.UI...
✅ Build succeeded.
```

## Runtime Verification

After launch:
1. ✅ System tray icon appears (WinForms NotifyIcon)
2. ✅ Balloon notification shows (WinForms)
3. ✅ Press Ctrl+Shift+P → Search overlay appears (WPF Window)
4. ✅ Type to search → Results appear (WPF ListBox)
5. ✅ Arrow keys navigate (WPF KeyEventArgs)
6. ✅ Enter opens folder (WPF)
7. ✅ Escape closes overlay (WPF)

## Best Practices

When mixing WPF and WinForms:

1. **Use WPF for UI** - Modern, better performance, better styling
2. **Use WinForms for legacy/specific features** - NotifyIcon, certain dialogs
3. **Fully qualify ambiguous types** - Prevents build errors
4. **Keep frameworks separated** - Don't mix unnecessarily
5. **Document the hybrid approach** - Help future maintainers

## Common Pitfalls

❌ **Don't do this:**
```csharp
using System.Windows.Forms;  // Causes ambiguity
using System.Windows;

public class MyClass : Application  // Which Application?
```

✅ **Do this instead:**
```csharp
// No using statements for ambiguous namespaces

public class MyClass : System.Windows.Application  // Clear!
```

## Performance Impact

Fully qualified names have **zero runtime performance impact**. The compiler resolves them at compile time, so the generated IL is identical to using short names.
