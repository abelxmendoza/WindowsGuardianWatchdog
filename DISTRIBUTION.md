# Download and trust

Users install one file: `WindowsGuardianWatchdog.msix`. It already contains the app and the .NET runtime. Windows checks its signature before installation and shows the publisher name from that signature.

An unsigned zip is not a public download. Windows SmartScreen and Microsoft treat an unsigned or self-signed file as untrusted.

## What you give users

After a signed build, `dist\release` contains:

- `WindowsGuardianWatchdog.msix` — the installer. Users double-click it.
- `SHA256SUMS.txt` — the SHA256 they can compare before opening the file.
- `INSTALL.txt` — the short instructions that go next to the download.
- `WindowsGuardianWatchdog.appinstaller` — only when a public HTTPS download address is supplied. Opening that file installs the app and checks for updates.

Host those files on HTTPS. The usual place is a GitHub Release.

## Build a release Windows will trust

Use a code-signing certificate from a public certificate authority, or Microsoft Trusted Signing. Keep the private key out of this repository. Put the PFX password in `WG_SIGNING_PASSWORD`, not in the command line and not in git.

```powershell
$env:WG_SIGNING_PASSWORD = "<password>"
.\Publish-Release.ps1 `
  -CertificatePath C:\secure\codesign.pfx `
  -DownloadBaseUrl "https://github.com/<owner>/<repo>/releases/download/v1.0.0"
```

The build signs `WindowsGuardianWatchdog.exe` and the MSIX with SHA256, timestamps the signature through Microsoft, and runs `signtool verify /pa`. That check is Windows' own trust decision. If the certificate does not chain to a root Windows trusts, the build stops.

A certificate already in the Windows certificate store can be selected by thumbprint:

```powershell
.\Publish-Release.ps1 -CertificateThumbprint "<thumbprint>" -DownloadBaseUrl "https://example.com/guardian"
```

Microsoft Trusted Signing uses the Azure signing library instead of a PFX. The publisher string has to be the certificate subject, copied exactly:

```powershell
.\Publish-Release.ps1 `
  -TrustedSigningDlib "C:\tools\Azure.CodeSigning.Dlib.dll" `
  -TrustedSigningMetadata "C:\secure\trusted-signing.json" `
  -Publisher "CN=Your Name, O=Your Company, C=US" `
  -DownloadBaseUrl "https://example.com/guardian"
```

`-AllowUnsigned` builds a package for a local test only. Do not put that file on a download page.

## What users see

1. They download `WindowsGuardianWatchdog.msix`.
2. They compare it with the published checksum:

```powershell
certutil -hashfile .\WindowsGuardianWatchdog.msix SHA256
```

3. They double-click the MSIX. Windows shows the publisher from the signature.
4. The first launch asks for administrator approval. The publisher on that prompt is the same certificate. The app needs that approval to read Defender, the firewall, and Windows Update.

A brand-new publisher can still get a SmartScreen prompt until the signed file has reputation. Microsoft Trusted Signing and the Microsoft Store are the two ways Microsoft builds that reputation. Signing with a self-signed certificate does not.

## GitHub Release

The workflow `.github/workflows/release.yml` runs when you start it manually from the Actions tab. Add these repository secrets first:

- `SIGNING_CERT_BASE64` — the PFX, encoded as Base64
- `SIGNING_CERT_PASSWORD` — the PFX password

The workflow refuses to publish when those secrets are missing. It uploads the MSIX, the checksum, the installer text, and the app installer to a GitHub Release named from the version in `WindowsGuardianWatchdog.csproj`.

## Microsoft Store

Store distribution is the path Microsoft signs itself.

1. Open [Partner Center](https://partner.microsoft.com/dashboard) and reserve the name Windows Guardian Watchdog.
2. Copy the app's Publisher identity from Partner Center. It looks like `CN=00000000-0000-0000-0000-000000000000`.
3. Build with `-Publisher` set to that exact value, then upload the MSIX in the submission.
4. In the listing, use the same description as the package: local security monitoring, with no account and no telemetry. Point the privacy policy at the `Privacy.txt` included in the package, or at a page with that same text.
5. Submit for certification. Microsoft re-signs the Store package. Users then install it from the Store instead of a sideloaded file.

The package already declares the full-trust desktop capability, Windows 10 version 2004 as the minimum (an elevated Windows App SDK app also needs update KB5013942), and Windows 11 version 24H2 as the tested version.

## Local test build

```powershell
.\Publish-Release.ps1 -AllowUnsigned
```

Windows will warn or block that file. It is only for checking that the package opens on your own PC.
