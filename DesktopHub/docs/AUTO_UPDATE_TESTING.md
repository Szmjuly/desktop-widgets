# Auto-Update Testing Guide

## 🧪 How to Test Auto-Update

### **Scenario 1: Test Full Workflow**

**Setup:**

1. **Build current version (v1.0.0):**
   ```powershell
   $env:PATH = "C:\dotnet;$env:PATH"
   dotnet build
   ```

2. **Update Firebase to v1.0.1:**
   ```powershell
   cd scripts
   .\update-version.ps1 "1.0.1" "Test auto-update functionality"
   ```

3. **Set download URL to test file:**
   - In Firebase Console, set `download_url` to a direct exe download link
   - For testing, you can use the current build exe path as a file:// URL
   - Or upload to GitHub releases first

**Test:**

1. Run the v1.0.0 build
2. Right-click tray → "Check for Updates"
3. Should see: "Version 1.0.1 is available! The update will download and install automatically."
4. Click Yes
5. Watch notifications:
   - "Downloading update..."
   - "Update downloaded! Installing..."
   - "Update ready to install! DesktopHub will restart."
6. Click OK
7. App should close and restart automatically

**Verify:**
- New version is running (check About or logs)
- Old exe is replaced
- No leftover temp files

---

### **Scenario 2: Test with Real GitHub Release**

**Setup:**

1. **Create two versions:**
   ```powershell
   # Edit version to 1.0.0
   dotnet publish src\DesktopHub.UI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
   # Rename: DesktopHub-1.0.0.exe
   
   # Edit version to 1.0.1
   dotnet publish src\DesktopHub.UI -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
   # Rename: DesktopHub-1.0.1.exe
   ```

2. **Upload v1.0.1 to GitHub:**
   ```powershell
   gh release create v1.0.1 DesktopHub-1.0.1.exe --title "v1.0.1" --notes "Auto-update test"
   ```

3. **Get direct download URL:**
   - Go to: https://github.com/yourusername/desktophub/releases/tag/v1.0.1
   - Right-click the exe → Copy link address
   - Should be like: `https://github.com/yourusername/desktophub/releases/download/v1.0.1/DesktopHub-1.0.1.exe`

4. **Update Firebase:**
   ```powershell
   # Use the direct download link
   .\update-version.ps1 "1.0.1" "Auto-update test"
   # Then manually edit download_url in Firebase Console to the direct link
   ```

**Test:**
- Run DesktopHub-1.0.0.exe
- Check for updates
- Should download from GitHub and install

---

### **Scenario 3: Test Error Handling**

**Test download failure:**
```powershell
# Set invalid download URL in Firebase
download_url: "https://invalid-url.com/app.exe"
```
- Should show: "Update failed. Please try again later."

**Test network timeout:**
- Disconnect internet during download
- Should show error notification

**Test permissions:**
- Run from a read-only folder
- Should fail gracefully with error message

---

## 🔧 Quick Test Script

```powershell
# test-update.ps1
param([string]$FromVersion = "1.0.0", [string]$ToVersion = "1.0.1")

Write-Host "Testing auto-update from v$FromVersion to v$ToVersion" -ForegroundColor Cyan

# 1. Set Firebase to new version
.\update-version.ps1 $ToVersion "Test update from v$FromVersion"

# 2. Set download URL to current exe (for quick testing)
$currentExe = (Get-Item ..\src\DesktopHub.UI\bin\Debug\net8.0-windows\DesktopHub.exe).FullName
Write-Host "Using test exe: $currentExe"

# 3. Instructions
Write-Host "`nNow:" -ForegroundColor Green
Write-Host "1. Run DesktopHub"
Write-Host "2. Right-click tray -> Check for Updates"
Write-Host "3. Click Yes to test auto-update"
Write-Host "`nExpected: App downloads, installs, and restarts automatically"
```

---

## 📋 Checklist

- [ ] Download URL is a **direct link** to exe (not a GitHub release page)
- [ ] URL returns `Content-Type: application/octet-stream` or `application/x-msdownload`
- [ ] Exe is accessible without authentication
- [ ] Current user has write permissions to app folder
- [ ] No antivirus blocking the update
- [ ] Batch script can run (execution policy allows it)

---

## 🐛 Common Issues

### **"Update failed" immediately**
- Check download_url in Firebase is a direct link, not a webpage
- GitHub releases: Use the "download" link, not the "tag" or "release" page

**Wrong URL:**
```
https://github.com/user/repo/releases/tag/v1.0.1  ❌
```

**Correct URL:**
```
https://github.com/user/repo/releases/download/v1.0.1/DesktopHub.exe  ✅
```

### **Download hangs**
- File might be too large (>100MB)
- Network timeout (default is 5 minutes)
- Check logs: `logs/debug.log`

### **App doesn't restart**
- Batch script might be blocked
- Check temp folder: `%TEMP%\DesktopHub-Update.bat`
- Run batch manually to test

### **Permission denied**
- App might be running from Program Files (needs admin)
- Move to user folder: `%LOCALAPPDATA%\DesktopHub`

---

## 🎯 Production Checklist

Before releasing auto-update to users:

- [ ] Test with multiple versions (1.0.0 → 1.0.1 → 1.0.2)
- [ ] Test on clean Windows machine
- [ ] Test with antivirus enabled (Windows Defender, Norton, etc.)
- [ ] Test from different install locations (Desktop, Program Files, AppData)
- [ ] Test with slow internet connection
- [ ] Test update cancellation (click No or Cancel)
- [ ] Verify old exe is actually replaced
- [ ] Verify no temp file leftovers
- [ ] Test error handling (bad URL, network failure)
- [ ] Add logging for all update steps
- [ ] Consider adding update changelog in app
- [ ] Add "Check for updates on startup" option
- [ ] Consider background auto-update (check every 24h)

---

## 🔐 Security Considerations

### **Code Signing (Recommended for Production)**

Without code signing:
- ⚠️ Windows SmartScreen warnings
- ⚠️ Antivirus may flag or block
- ⚠️ Users see "Unknown publisher"

With code signing:
- ✅ No warnings
- ✅ Shows your company name
- ✅ Higher trust level

**Get a certificate:**
1. Purchase from DigiCert, Sectigo (~$100-400/year)
2. Sign your exe:
   ```powershell
   signtool sign /f cert.pfx /p password /t http://timestamp.digicert.com DesktopHub.exe
   ```

### **Download Verification**

Add checksum verification:

```csharp
// In Firebase:
{
  "latest_version": "1.0.1",
  "download_url": "...",
  "sha256": "abc123..."  // SHA256 hash of exe
}

// In code:
var downloadedHash = ComputeSHA256(updateData);
if (downloadedHash != updateInfo.Sha256)
{
    throw new Exception("Update file verification failed");
}
```

This prevents man-in-the-middle attacks and corrupted downloads.

---

## 📊 Update Analytics

The app already logs update checks to Firebase (`update_checks` node).

**Track these metrics:**
- How many users check for updates
- How many downloads succeed vs fail
- How many complete installation
- Which versions users are on

**Add to `UpdateInfo` model:**
```csharp
public bool InstallSucceeded { get; set; }
public string ErrorMessage { get; set; }
```

**Log installation success:**
```csharp
// After restart, log success
await firebaseService.LogUsageEventAsync("update_installed", new Dictionary<string, object>
{
    ["from_version"] = "1.0.0",
    ["to_version"] = "1.0.1",
    ["success"] = true
});
```

This helps you identify update problems in the field.
