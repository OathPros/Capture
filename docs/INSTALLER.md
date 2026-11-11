# YU Capture Windows installer

## User experience

Download one `YU-Capture-<version>-Setup-x64.exe`, run the setup wizard, then launch YU Capture. A Start menu shortcut is created, with an optional desktop shortcut. Files install to `%LOCALAPPDATA%\Programs\YU Capture`. No separate .NET download, ZIP extraction, or account is required.

Setup supports Windows 11 x64, installs for the current user, and registers an uninstaller in Windows Settings. The stable Inno Setup AppId and destination support installing future versions over the existing installation. Exit Capture from its tray icon before upgrading. Existing files in use are handled by Inno Setup's close-applications support. Automatic updates and downgrade prevention are not implemented; older versions may not read newer project schemas.

Saved projects and preferences remain in `%LOCALAPPDATA%\Capture`, outside the install directory. The installer does not remove that folder on uninstall.

## Prerequisites

The packaging script bundles Microsoft's Evergreen WebView2 bootstrapper after validating its Microsoft Authenticode signature. Setup checks per-user and machine-wide WebView2 runtime registration. If missing, it runs the bootstrapper and checks registration again before copying app files. A failed runtime installation stops setup with a retry/contact-IT message. Downloading the runtime requires internet access; institutional controls may restrict it. An offline WebView2 runtime is not bundled.

The YU Capture installer and app remain unsigned. A typical wizard does not eliminate SmartScreen or publisher warnings. Production distribution still needs an organizational signing certificate and signing of the app and installer. No signing credentials are included or fabricated.

## Build and release

On Windows, install Inno Setup 6 in its default directory, then run the existing build/test scripts followed by `scripts/package.ps1`. CI uses the runner’s installed Inno Setup 6 compiler, installing it only when absent. Packaging publishes the self-contained app, retains the portable ZIP, builds the EXE and reports its SHA-256 hash. The EXE version comes from Capture.App.csproj.

Use decimal version increments for releases: `0.7`, `0.8`, `0.9`, then `1.0`. Update only the `<Version>` in `src/Capture.App/Capture.App.csproj`; the editor bridge version, installer metadata, portable ZIP name, and release tag/artifacts derive from it.

CI runs `scripts/test-installer.ps1` after the existing packaged-app smoke test. This performs silent install, repeated installation, file verification, launch of the installed app's real WebView2 screenshot smoke mode, and uninstall while verifying a user-created sentinel file survives. Main-branch pushes and matching version tags publish both EXE and ZIP after the Windows job passes. Existing release assets are preserved; new releases require a new app version. The download page prefers installer assets over ZIP assets and updates its primary button automatically. The older ZIP remains explicitly available until an installer is actually published.

## Verification and remaining gates

The Linux development environment cannot compile Inno Setup or run Windows installers. Source checks and simulated download-page checks are available here; Windows CI must establish installer build and execution results. Windows PR run 41833015580 subsequently passed the full app build and tests, installer compilation, installation, repeat installation, installed-app WebView2 screenshot smoke test, and uninstall. Main run 41833444333 repeated and passed those checks, then published preview.2 with the Setup EXE and ZIP. The release asset was confirmed uploaded through GitHub.

Before distribution, manually check a clean Windows 11 user account without WebView2, a disconnected machine, institutional installation restrictions, install/update while Capture is running, Start menu/desktop shortcuts, Windows Settings uninstall, project preservation, display scaling, and unsigned-publisher prompts. CI machines commonly already have WebView2, so their install tests alone do not verify prerequisite installation.
