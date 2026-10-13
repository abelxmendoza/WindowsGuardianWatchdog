# Omega Technologies Dark Theme

This theme implements the MacGuardian Watchdog "Omega Technologies Dark" cyberpunk aesthetic for Windows Guardian Watchdog.

## Color Palette

### Primary Colors
- **Electric Purple** (#8A2BE2) - Primary actions, headers, highlights
- **Neon Red** (#FF0044) - Alerts, errors, danger outputs
- **Cyan Glow** (#00FFFF) - Selection, cursor, interactive elements

### Text Colors
- **Dim Gray** (#808080) - Regular text, body content
- **Light Gray** (#E6E6E6) - Primary text
- **Secondary Gray** (#999999) - Secondary text

### Background Colors
- **Deep Black** (#000000) - Base background
- **Background Noise** (#0A0A0A) - Subtle background variation
- **Card Background** (#1A1A1A) - Card/panel backgrounds

### Theme Variants
- **Theme Purple** (#8C00FF) - Main theme color
- **Theme Purple Dark** (#5900A6) - Darker variant
- **Theme Purple Light** (#B333FF) - Lighter variant

## Usage

The theme is automatically applied via `App.xaml` which merges `OmegaDarkTheme.xaml`.

All views use the theme colors through static resources:
- `{StaticResource PrimaryPurpleBrush}`
- `{StaticResource SecondaryRedBrush}`
- `{StaticResource AccentCyanBrush}`
- `{StaticResource TextPrimaryBrush}`
- `{StaticResource BackgroundBlackBrush}`
- etc.

## Style Resources

### Buttons
- `PrimaryButtonStyle` - Purple buttons for primary actions
- `DangerButtonStyle` - Red buttons for dangerous actions
- `AccentButtonStyle` - Cyan buttons for accent actions

### Text
- `TitleTextStyle` - Large purple titles
- `SubtitleTextStyle` - Medium gray subtitles
- `BodyTextStyle` - Regular gray body text
- `AlertTextStyle` - Red alert text
- `SuccessTextStyle` - Green success text

### Components
- `CardStyle` - Dark card with purple border
- `DarkListViewStyle` - Dark list views
- `DarkTextBoxStyle` - Dark text boxes
- `CyanProgressBarStyle` - Cyan progress bars

## Typography

- **Titles**: Bold, 32px, 24px, 20px (Purple)
- **Subtitles**: SemiBold, 18px, 16px (Light Gray)
- **Body**: Regular, 14px (Gray)
- **Captions**: Small, 12px (Secondary Gray)

## Design Principles

1. **Dark First**: Deep black backgrounds with subtle variations
2. **High Contrast**: Bright colors on dark backgrounds for visibility
3. **Cyberpunk Aesthetic**: Purple, red, and cyan create the security console feel
4. **Consistent Spacing**: 20px padding, 10px spacing between elements
5. **Glowing Effects**: Subtle shadows and glows on interactive elements

