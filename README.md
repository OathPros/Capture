# CAPTURE — Windows preview

CAPTURE records mouse interactions across Windows applications and turns them into an editable local guide with screenshots and click highlights. This repository now includes application source, a bundled React editor, tests, and Windows packaging, with Word, Markdown and self-contained HTML exports.

**Early preview, not the completed V1 specification.** The application compiles and deterministic tests run, but interactive Windows capture has not been verified in this Linux development session. Use synthetic data first. See [current status](docs/STATUS.md) and [manual checks](docs/MANUAL-TESTS.md).

## Download and run

1. Open this repository's **Releases** and choose the latest Windows preview. Download `Capture-0.3.0-preview.1-win-x64.zip`. If no release has been produced yet, open **Actions → Build Windows preview**, choose a successful run, and download the `Capture-Windows-x64-preview` artifact.
   The optional `Capture-export-examples.zip` download contains synthetic sample DOCX, HTML and Markdown outputs for review.
2. Extract the entire ZIP to a folder you control. Run **Capture.App.exe**; keep the companion DLLs and `editor` folder together.
3. Exit any older Capture instance from the tray before replacing files or opening the new version. Existing 1.0 guide metadata is backed up as `guide.v1.0.backup.json` when first saved as schema 1.1; older app versions cannot read the upgraded schema. Screenshot files stay unchanged until you explicitly apply pixel edits.
4. Requires **Windows 11 x64** and the **Microsoft Edge WebView2 Evergreen Runtime**. The .NET runtime is included; you do not need Visual Studio, Node or the .NET SDK to run the ZIP. Install WebView2 from Microsoft's official site if startup reports it missing: https://developer.microsoft.com/microsoft-edge/webview2/ .
5. This preview is unsigned. Windows or your organization's policy may block it. Follow your organization's approval process; do not bypass enterprise controls.

## Record, edit and export

- Choose **Record workflow**, read the capture notice and confirm. The main editor hides and a recording toolbar appears. Alternatively use **Ctrl+Shift+R**, including to finish. If occupied, change the letter in Settings or use the tray/menu controls.
- Perform a short workflow using fictional data in ordinary applications. Clicks create ordered steps from recent screenshots of the active monitor. This prototype uses GDI capture; protected surfaces, elevated apps, unusual display drivers and monitors larger than 4K may not work.
- Use **Pause** before sensitive content; **Resume** starts capture again. **Finish** saves and opens the guide. **Cancel** asks before discarding the recording and its project assets.
- Edit the title, introduction, steps and instructions. Add text steps, delete, duplicate, reorder and undo/redo. The screenshot editor includes Select, Crop, Blur, Redact, Text, Line, Arrow, Callout, Rectangle, Ellipse and Highlight tools. Drag to draw; select to move or resize with handles. Adjust fill, outline colour/width, text colour/font size and shadow. Cut/copy/paste works on annotations; Crop trims image edges. Zoom and exact geometry controls are available. Save runs after a short delay; **Save** or **Ctrl+S** also saves. Review each screenshot and mark reviewed steps.
- **Word DOCX** creates a Word document without requiring Word to be installed: title, introduction, numbered headings, instructions, notes, page numbers and embedded screenshots sized for Letter paper.
- **HTML guide** creates one styled `.html` file with embedded screenshots, a linked contents list, responsive layout and print styles. Open it offline in a browser; no assets folder or web server is needed.
- **Markdown ZIP** includes `README.md`, ordered instructions, notes and an `assets` folder of screenshots. Extract the complete ZIP before opening the Markdown. All three formats flatten highlight outlines into export copies; source project images remain editable.
- **Portable package** includes editable JSON and original screenshot assets plus annotation geometry; import it on the Projects screen. Canceling an export leaves existing files alone; replacement requires a native file confirmation.
- Reopen drafts from **Projects**. Saved capture steps are discoverable after a crash. Storage: `%LOCALAPPDATA%\Capture\Projects\{guideId}\guide.json` plus `assets`. Preferences and WebView2 data live under `%LOCALAPPDATA%\Capture`. Back up complete project folders.
- Closing the main window keeps Capture in the tray. Use the tray **Exit** command to shut down and unregister the hotkey. Capture flushes pending editor saves before recording or exiting.

If a screenshot cannot load, the editor shows a specific error and **Retry screenshot**. Screenshots are read and checksum-validated by the native host; they no longer depend on a separate WebView2 image origin. Restore genuinely missing files from a complete project backup/package.

## Privacy and limitations

No sign-in, server, AI, telemetry, screenshot upload or raw keystroke logging. UI Automation reads limited control metadata and never requests editable values. Password controls are suppressed when identified, but **screenshots may still contain passwords, private browser content or other sensitive information elsewhere on the monitor**. Process exclusions apply to the foreground/clicked application, not every visible window. Private browser windows are not automatically recognized. Screenshots are not encrypted by Capture; they rely on your Windows account permissions and disk protections.

Use synthetic data for the preview. Pause around sensitive material and inspect all screenshots. **Blur is visual softening, not secure redaction.** Use solid Redact regions for secrets. Before any export, choose **Apply blur/redaction permanently** and confirm: this burns every blur/redaction region for the shared screenshot into a new asset, updates all steps using it, removes the original managed file and clears Undo. A failed cleanup blocks export until **Finish privacy cleanup** succeeds. External exports/backups and forensic disk remnants are not erased. Crop is reversible and is not redaction; portable packages retain uncropped originals for ordinary crop edits. PDF, manual image insertion, SQLite indexing, signing and institutional publishing remain unfinished. The full requirements remain in CAPTURE_CODEX_BUILD_GUIDE.md; they are not claims about this release.

If a UIA provider hangs, finishing stops waiting after ten seconds and tells you to restart. Already saved drafts remain available. A hung background worker cannot be safely canceled inside its COM call; restart before another recording. Guides exceeding 10,000 steps or packages over 512 MB are rejected. Monitor sampling uses at most two recent PNG frames (one active monitor at a time).

## Build from source

Windows prerequisites: .NET **10.0.403** SDK (or compatible patch via global.json), Node **24 LTS**, Git, WebView2 Runtime, PowerShell.

```powershell
./scripts/build.ps1
./scripts/test.ps1
./scripts/dev.ps1
./scripts/package.ps1
./scripts/smoke-test.ps1
```

CI builds on Windows, runs core, Windows image-rendering and browser tests and uploads a self-contained x64 ZIP. A `v*` tag also creates a clearly marked prerelease after those checks pass. CI also launches the packaged WebView2 app with a temporary synthetic project and waits for its actual screenshot to decode through the native asset bridge. This does not prove interactive desktop capture reliability; complete the manual matrix before real-world use.

Linux can compile Windows-targeted projects and run the core/editor tests, but cannot launch WPF. Install the pinned .NET SDK, then `npm ci && npm run build` under `src/capture-editor`; run `dotnet restore Capture.slnx --locked-mode`, `dotnet build Capture.slnx -c Release --no-restore`, `dotnet test tests/Capture.Core.Tests -c Release --no-restore`. Run `npm test` and `npm run test:e2e` in the editor directory. Set `CAPTURE_CHROMIUM_PATH` to an installed Chromium executable if browser downloads are unavailable.

## License

No project license has been selected; no rights beyond applicable law are granted by this repository. Third-party packages retain their own licenses; see docs/DEPENDENCIES.md. Choose a project license before wider distribution.
