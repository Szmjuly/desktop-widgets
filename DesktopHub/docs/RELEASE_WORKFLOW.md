# DesktopHub Release Workflow

## 📦 Complete Update Workflow

### **Current Implementation: Manual Download**

DesktopHub currently uses a **notification-based update system**:

1. ✅ App checks Firebase for new version
2. ✅ Shows notification if update available
3. ✅ Opens browser to download page
4. ❌ User manually downloads and installs
5. ❌ No auto-install

---

## 🔄 Developer Workflow (When You Release)

### **Step 1: Update Version Number**

Edit `src/DesktopHub.UI/DesktopHub.UI.csproj`:

```xml
<PropertyGroup>
  <Version>1.0.1</Version>  <!-- Change this -->
</PropertyGroup>
```

### **Step 2: Build Release**

```powershell
$env:PATH = "C:\dotnet;$env:PATH"

# Build release version
dotnet publish src\DesktopHub.UI `
  -c Release `
  -r win-x64 `
  --self-contained `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true

# Output: src\DesktopHub.UI\bin\Release\net8.0-windows\win-x64\publish\DesktopHub.exe
```

### **Step 3: Create GitHub Release**

```powershell
# Tag the version
git tag v1.0.1
git push origin v1.0.1

# Create release on GitHub
# Upload DesktopHub.exe as release asset
```

Or use GitHub CLI:
```powershell
gh release create v1.0.1 `
  src\DesktopHub.UI\bin\Release\net8.0-windows\win-x64\publish\DesktopHub.exe `
  --title "v1.0.1" `
  --notes "Bug fixes and improvements"
```

### **Step 4: Update Firebase**

```powershell
cd scripts
.\update-version.ps1 "1.0.1" "Bug fixes and improvements"
```

This updates Firebase with:
- New version number
- Release notes
- Download URL (auto-generated as `https://github.com/yourusername/desktophub/releases/tag/v1.0.1`)

### **Step 5: Test**

Run an older version and check for updates - should see notification!

---

## 👤 User Experience (Current)

### **What Users See:**

1. **User opens app** → Runs in background
2. **User clicks tray icon** → Right-click → "Check for Updates"
3. **If update available:**
   - Balloon notification: "Checking for updates..."
   - Dialog box appears:
     ```
     Update Available
     
     Version 1.0.1 is available!
     
     Current: 1.0.0
     
     Bug fixes and improvements
     
     [Yes] [No]
     ```
4. **If user clicks Yes:**
   - Browser opens to: `https://github.com/yourusername/desktophub/releases/tag/v1.0.1`
   - User downloads `DesktopHub.exe`
   - User runs the new exe
   - Old version exits, new version starts

5. **If no update:**
   - Balloon notification: "You're up to date! (v1.0.0)"

---

## 🚀 Auto-Update Options

Currently, DesktopHub **does NOT auto-install**. Here are options to add it:

### **Option 1: Simple Auto-Download** (Recommended)

Download update in background, notify when ready:

**Pros:**
- ✅ Faster for users
- ✅ Still gives user control
- ✅ Simple to implement

**Cons:**
- ⚠️ User still has to click to install

**Implementation:**
```csharp
// In TrayIcon.cs CheckForUpdates()
if (updateAvailable)
{
    // Download in background
    var tempPath = Path.Combine(Path.GetTempPath(), "DesktopHub-Update.exe");
    using var client = new HttpClient();
    var updateData = await client.GetByteArrayAsync(updateInfo.DownloadUrl);
    await File.WriteAllBytesAsync(tempPath, updateData);
    
    // Notify user
    var result = MessageBox.Show(
        "Update downloaded! Install now?",
        "Update Ready",
        MessageBoxButton.YesNo
    );
    
    if (result == MessageBoxResult.Yes)
    {
        Process.Start(tempPath);
        Application.Current.Shutdown();
    }
}
```

### **Option 2: Full Auto-Update** (Like Chrome)

Download and install automatically, restart app:

**Pros:**
- ✅ Seamless user experience
- ✅ Users always on latest version

**Cons:**
- ⚠️ Can interrupt user's work
- ⚠️ More complex (needs updater service)
- ⚠️ Must handle running instances

**Implementation:**
Use a library like **Squirrel.Windows** or **Velopack**:

```powershell
# Add NuGet package
dotnet add package Velopack
```

```csharp
// In App.xaml.cs
var updateManager = new UpdateManager(
    "https://github.com/yourusername/desktophub/releases"
);

await updateManager.UpdateApp();
```

### **Option 3: Background Updater** (Professional)

Separate updater process that runs independently:

**Pros:**
- ✅ Most reliable
- ✅ Can update while app is closed
- ✅ Handles permissions properly

**Cons:**
- ⚠️ Complex to implement
- ⚠️ Requires separate updater.exe
- ⚠️ Windows may show UAC prompt

---

## 📊 Comparison

| Feature | Current | Auto-Download | Full Auto | Updater Service |
|---------|---------|---------------|-----------|-----------------|
| User clicks update | ✅ | ✅ | ❌ | ❌ |
| Opens browser | ✅ | ❌ | ❌ | ❌ |
| Auto-downloads | ❌ | ✅ | ✅ | ✅ |
| Auto-installs | ❌ | ❌ | ✅ | ✅ |
| Complexity | Simple | Easy | Medium | Hard |
| User control | Full | High | Low | Medium |

---

## 🎯 Recommended Approach

### **For Now: Keep Current System**

The current "open browser" approach is:
- ✅ Simple and reliable
- ✅ Familiar to users
- ✅ No code signing required
- ✅ Works on all Windows versions

### **Future Enhancement: Add Auto-Download**

When you want better UX:
1. Download update in background
2. Show "Update ready!" notification
3. User clicks to install
4. New version replaces old

This is the **sweet spot** - better UX without complexity.

### **Production: Use Velopack**

When deploying to many users:
1. Use Velopack for proper auto-updates
2. Sign your executables (avoid SmartScreen warnings)
3. Host releases on GitHub Releases or Azure
4. Add crash reporting

---

## 🔐 Code Signing (Important for Distribution)

**Without code signing:**
- ⚠️ Windows SmartScreen shows warning
- ⚠️ Users see "Unknown publisher"
- ⚠️ Downloads may be blocked

**With code signing:**
- ✅ No SmartScreen warnings
- ✅ Shows your company name
- ✅ Higher trust

**Get a certificate:**
1. Purchase from DigiCert, Sectigo, etc. (~$100-400/year)
2. Or use **Azure Code Signing** (cloud-based)

**Sign your exe:**
```powershell
signtool sign /f certificate.pfx /p password DesktopHub.exe
```

---

## 📝 Release Checklist

### **Pre-Release**
- [ ] Update version in `.csproj`
- [ ] Test all features
- [ ] Run on clean machine
- [ ] Check logs for errors
- [ ] Verify Firebase connectivity

### **Build**
- [ ] Build in Release mode
- [ ] Test the release exe
- [ ] Check file size (<50MB ideal)
- [ ] Scan for viruses (VirusTotal)

### **Publish**
- [ ] Create Git tag
- [ ] Push tag to GitHub
- [ ] Create GitHub release
- [ ] Upload exe to release
- [ ] Write release notes

### **Update Firebase**
- [ ] Run `update-version.ps1`
- [ ] Verify in Firebase Console
- [ ] Test update check from old version

### **Post-Release**
- [ ] Announce update (Discord, Twitter, etc.)
- [ ] Monitor error logs in Firebase
- [ ] Watch for user reports
- [ ] Update documentation

---

## 🛠️ Automation (Advanced)

### **GitHub Actions: Auto-Release**

Create `.github/workflows/release.yml`:

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
      
      - name: Publish
        run: |
          dotnet publish src/DesktopHub.UI `
            -c Release `
            -r win-x64 `
            --self-contained `
            -p:PublishSingleFile=true
      
      - name: Create Release
        uses: softprops/action-gh-release@v1
        with:
          files: src/DesktopHub.UI/bin/Release/net8.0-windows/win-x64/publish/DesktopHub.exe
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      
      - name: Update Firebase
        run: |
          $version = "${{ github.ref_name }}".TrimStart('v')
          scripts/update-version.ps1 $version "See release notes"
```

**Usage:**
```powershell
git tag v1.0.1
git push origin v1.0.1
# GitHub Actions automatically builds and releases!
```

---

## 🎯 Your Current Setup

Based on the Firebase screenshot, you have:

```
app_versions/desktophub/
├── download_url: "https://github.com/yourusername/desktophub/releases/tag/v1.0.0"
├── latest_version: "1.0.0"
├── release_date: "2026-02-02T14:35:05Z"
├── release_notes: "Initial release"
└── required_update: false
```

**This means:**
1. Users on v1.0.0 will see "You're up to date!"
2. Users on v0.9.x will see update notification
3. Clicking "Yes" opens the GitHub releases page
4. User downloads and runs the new exe manually

**To release v1.0.1:**
1. Update version in csproj → `1.0.1`
2. Build release
3. Upload to GitHub releases
4. Run: `.\update-version.ps1 "1.0.1" "Bug fixes"`
5. Done! Users will see update available

---

## Summary

**Current workflow:**
- Developer: Build → Upload to GitHub → Run script → Update Firebase
- User: Check for updates → See notification → Click yes → Browser opens → Download → Install manually

**No auto-install yet**, but it's easy to add if you want it later!
