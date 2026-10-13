# Logo Setup Instructions

## Adding Your Logo Image

The Windows Guardian Watchdog logo has been integrated throughout the application. To complete the setup:

### Step 1: Add the Logo File

1. Place your logo image file in the `Resources` folder
2. Name it exactly: `Logo.png`
3. Recommended specifications:
   - **Format**: PNG with transparency support
   - **Minimum size**: 256x256 pixels
   - **Recommended sizes**:
     - 256x256 (app icon)
     - 512x512 (splash screen)
     - 1024x1024 (store submission)

### Step 2: Logo Design Specifications

Based on your logo design, it should feature:
- **Main element**: Fierce wolf/dog head with Omega (Ω) symbol on forehead
- **Windows logo**: Four-paned Windows symbol held in the jaws
- **Color scheme**: 
  - Primary: Purple (#9D4EDD, #C77DFF)
  - Accent: Red (for eyes and lightning)
  - Background: Dark purple/black
- **Text**: "WINDOWSGUARDIAN WATCHDOG" (can be part of image or separate)

### Step 3: Where the Logo Appears

Once added, the logo will automatically appear in:

1. **Navigation Header** - Main window navigation bar (60x60px)
2. **Dashboard** - Large logo with branding (120x120px)
3. **All Views** - Page headers (64x64px)
4. **App Icon** - Taskbar and Start menu
5. **Splash Screen** - App startup screen
6. **Package Manifest** - App store listing

### Step 4: Verify Integration

After adding the logo file:

1. Build the project: `dotnet build`
2. Run the app: `dotnet run`
3. Check that the logo appears in:
   - Navigation header
   - Dashboard view
   - All page headers

### Troubleshooting

If the logo doesn't appear:

1. **Check file name**: Must be exactly `Logo.png` (case-sensitive)
2. **Check file location**: Must be in `Resources\Logo.png`
3. **Check file format**: Must be PNG format
4. **Rebuild project**: Clean and rebuild after adding the file
5. **Check XAML**: All views reference `ms-appx:///Resources/Logo.png`

### File Structure

```
WindowsGuardianWatchdog/
├── Resources/
│   ├── Logo.png          ← Add your logo here
│   └── README.md
└── ...
```

The logo is referenced throughout the app using the URI: `ms-appx:///Resources/Logo.png`

---

**Note**: The logo image file is not included in the repository. You need to add your actual logo PNG file to the `Resources` folder.

