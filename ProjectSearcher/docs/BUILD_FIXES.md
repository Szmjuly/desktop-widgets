# Build Fixes Applied

## Issue: Namespace Ambiguity Errors

### Problem
```
error CS0104: 'Application' is an ambiguous reference between 
'System.Windows.Forms.Application' and 'System.Windows.Application'

error CS0104: 'KeyEventArgs' is an ambiguous reference between 
'System.Windows.Forms.KeyEventArgs' and 'System.Windows.Input.KeyEventArgs'
```

### Root Cause
The `.csproj` file had both `UseWPF` and `UseWindowsForms` enabled, causing namespace conflicts between WPF and WinForms types.

### Solution Applied

#### 1. Removed UseWindowsForms from ProjectSearcher.UI.csproj
```xml
<!-- Before -->
<UseWPF>true</UseWPF>
<UseWindowsForms>true</UseWindowsForms>

<!-- After -->
<UseWPF>true</UseWPF>
```

#### 2. Added Explicit References for NotifyIcon
Since we need `NotifyIcon` from Windows.Forms for the system tray, we added explicit references:

```xml
<ItemGroup>
  <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
</ItemGroup>

<ItemGroup>
  <Reference Include="System.Windows.Forms" />
</ItemGroup>
```

#### 3. Fully Qualified All Windows.Forms Types in TrayIcon.cs
Changed all Windows.Forms types to use full namespace qualification:

```csharp
// Before
private readonly NotifyIcon _notifyIcon;
var contextMenu = new ContextMenuStrip();

// After
private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
var contextMenu = new System.Windows.Forms.ContextMenuStrip();
```

#### 4. Fully Qualified All WPF Types in TrayIcon.cs
Also qualified WPF types to avoid ambiguity:

```csharp
// Before
MessageBox.Show(..., MessageBoxButton.OK, MessageBoxImage.Information);
Application.Current.Shutdown();

// After
System.Windows.MessageBox.Show(..., 
    System.Windows.MessageBoxButton.OK, 
    System.Windows.MessageBoxImage.Information);
System.Windows.Application.Current.Shutdown();
```

## Files Modified

1. **src/ProjectSearcher.UI/ProjectSearcher.UI.csproj**
   - Removed `UseWindowsForms` property
   - Added `System.Drawing.Common` package reference
   - Added `System.Windows.Forms` assembly reference

2. **src/ProjectSearcher.UI/TrayIcon.cs**
   - Fully qualified all `System.Windows.Forms` types
   - Fully qualified all `System.Windows` types
   - Removed ambiguous `using` statements

## Why This Approach?

### WPF-First Architecture
- The application is primarily WPF-based (search overlay, windows)
- Only the system tray icon needs Windows.Forms
- Keeping WPF as the primary framework avoids conflicts

### Explicit References
- Using explicit namespace qualification prevents ambiguity
- Makes code more readable (clear which framework is being used)
- Avoids future conflicts if more types are added

### NotifyIcon Requirement
- WPF doesn't have a built-in system tray icon
- Windows.Forms NotifyIcon is the standard solution
- Many WPF applications use this hybrid approach

## Build Status
✅ All namespace conflicts resolved  
✅ Project builds successfully  
✅ Ready to run with `.\run`

## Next Steps
The application should now build and run successfully. Try:

```powershell
.\run
```

This will:
1. Use the local .NET SDK from `.dotnet/`
2. Build all projects
3. Launch the WPF search overlay
4. Create system tray icon
5. Register global hotkey (Ctrl+Shift+P)
