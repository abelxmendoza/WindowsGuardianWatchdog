# Distribution Guide

## 📦 Available Distribution Methods

Windows Guardian Watchdog can be distributed in multiple formats:

### 1. **ZIP Package** (Recommended for Manual Installation)
- Simple extraction and run
- Requires .NET 8.0 Runtime installed
- Includes installer script

### 2. **MSIX Package** (Modern Windows App)
- Modern Windows app package
- Can be installed via Microsoft Store or sideloading
- Automatic updates support
- Better Windows integration

### 3. **Portable Package** (Self-Contained)
- Includes .NET runtime
- No installation required
- Can run from USB drive
- Larger file size

## 🚀 Building Distribution Packages

### Quick Build (All Packages)

```powershell
# Build ZIP installer package
.\BuildInstaller.ps1

# Build MSIX package (requires Windows SDK)
.\CreateMSIXPackage.ps1

# Build portable package
.\CreatePortablePackage.ps1
```

### Step-by-Step

#### 1. ZIP Package (Standard Installer)

```powershell
.\BuildInstaller.ps1 -Configuration Release -OutputPath .\dist
```

**Output:**
- `dist\WindowsGuardianWatchdog-v1.0.0.zip` - Distribution ZIP
- `dist\WindowsGuardianWatchdog\` - Application files
- `dist\WindowsGuardianWatchdog\install.bat` - Installer script

**Installation:**
1. Extract ZIP file
2. Run `install.bat` (as Administrator) OR
3. Copy files to desired location and run `WindowsGuardianWatchdog.exe`

#### 2. MSIX Package (Modern App)

```powershell
.\CreateMSIXPackage.ps1 -Configuration Release
```

**Requirements:**
- Windows SDK 10.0.26100.0 or later
- MakeAppx.exe tool

**Output:**
- `dist\msix\WindowsGuardianWatchdog.msix` - MSIX package
- `dist\msix\INSTALL.txt` - Installation instructions

**Installation:**
```powershell
# Double-click .msix file OR
Add-AppxPackage -Path "WindowsGuardianWatchdog.msix"
```

**Note:** May require enabling "Sideload apps" in Windows Settings:
- Settings > Apps > Apps & features > Install apps from any source

#### 3. Portable Package (Self-Contained)

```powershell
.\CreatePortablePackage.ps1 -Configuration Release
```

**Output:**
- `dist\WindowsGuardianWatchdog-Portable-v1.0.0.zip` - Portable ZIP
- Includes .NET runtime (~100MB+)

**Installation:**
1. Extract ZIP to any location
2. Run `WindowsGuardianWatchdog.exe`
3. No installation required!

## 📋 Package Comparison

| Feature | ZIP Package | MSIX Package | Portable Package |
|---------|-------------|--------------|------------------|
| **Size** | ~5-10 MB | ~5-10 MB | ~100-150 MB |
| **.NET Runtime** | Required | Required | Included |
| **Installation** | Manual | Automatic | None |
| **Windows Integration** | Good | Excellent | Good |
| **Auto-Updates** | No | Yes | No |
| **USB Portable** | Yes | No | Yes |
| **Store Ready** | No | Yes | No |
| **Best For** | General use | Modern Windows | USB/Network |

## 🎯 Recommended Distribution

### For End Users:
**ZIP Package** - Simple, familiar, works everywhere

### For Modern Windows:
**MSIX Package** - Best Windows integration, auto-updates

### For Portable Use:
**Portable Package** - USB drives, network shares, no installation

## 📝 Distribution Checklist

Before distributing:

- [ ] Build all packages
- [ ] Test ZIP package installation
- [ ] Test MSIX package installation
- [ ] Test portable package
- [ ] Verify app icon appears correctly
- [ ] Test desktop shortcut creation
- [ ] Verify taskbar pinning works
- [ ] Check all features work after installation
- [ ] Update version numbers
- [ ] Create release notes

## 🔐 Code Signing (Optional but Recommended)

For production distribution, consider code signing:

```powershell
# Sign the executable
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com WindowsGuardianWatchdog.exe
```

## 📤 Publishing to Microsoft Store

To publish MSIX to Microsoft Store:

1. Create Microsoft Partner Center account
2. Create app submission
3. Upload MSIX package
4. Complete store listing
5. Submit for certification

## 🌐 GitHub Releases

For GitHub distribution:

1. Create a new release
2. Upload ZIP packages
3. Add release notes
4. Tag version (e.g., v1.0.0)

## 📊 File Sizes (Approximate)

- **ZIP Package**: 5-10 MB
- **MSIX Package**: 5-10 MB
- **Portable Package**: 100-150 MB (includes .NET runtime)

## 🛠️ Build Scripts

All build scripts support:
- `-Configuration` parameter (Debug/Release)
- `-OutputPath` parameter (custom output directory)

Example:
```powershell
.\BuildInstaller.ps1 -Configuration Release -OutputPath .\my-dist
```

---

**Ready to distribute! Choose the package format that best fits your needs.**

