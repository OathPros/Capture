# Icon update — 0.5.0-preview.2 (release validation pending)

- Preserved the user's supplied Windows icon kit in `assets/branding`; verified its seven 32-bit ICO frames and inspected the supplied PNG artwork.
- Embedded the multi-size icon into the Windows executable and WPF resources; main/recording windows and tray use the same artwork. Tray icon handles are disposed on shutdown. Inno Setup uses the icon, and installed shortcuts/Windows Settings use the executable's icon.
- Added supplied PNG artwork and favicon to the bundled editor and self-contained download page. Added branding assets to package/install smoke checks and existing browser theme validation.
- Local Release solution build passes with zero warnings/errors; all seven supplied ICO image frames are present in the built EXE. Eight editor unit tests and 17 browser tests pass. Header artwork was inspected in dark/light previews; the standalone page logo/favicon decode successfully. Windows packaging, embedded WPF resource loading and installed-app checks are pending CI. Interactive taskbar/tray appearance at mixed DPI still requires Windows visual inspection.

---

# Current version — 0.5.0-preview.1

## Implemented
- Native local PNG/JPEG/BMP chooser behind the allowlisted `asset.import` bridge. No browser-supplied filesystem paths. Checks project identity, idle state, file size, decoded dimensions and managed PNG validation. Re-encodes pixels without source metadata, saves a checksummed local asset and rolls back the new file on save failure.
- Add image step inserts after the selected step, including empty guides. Existing steps offer Add image, Replace image and Remove image. Replacement preserves text, resets old annotations/crop/cuts and marks the step for review. Ordinary undo retains the managed asset manifest and restores attachments/markup. Native pixel sanitization still clears history.
- Imported images use the existing annotation, preview, autosave, clipboard and export pipelines. No schema change. Source file stays unchanged; remove/replace are reversible edits and do not securely delete old project assets.
- App/bridge/package/CI artifact versions advanced to 0.5.0-preview.1; published download links still refer to the existing released installer.

## Screenshot, appearance and export additions
- One-off monitor screenshot command from the editor, selected step, tray and recording toolbar. Ctrl+Shift+S is separately configurable from Ctrl+Shift+R. Saved settings without a screenshot key migrate automatically, including choosing P when an existing recording key uses S. Shortcut conflicts/save failures restore previous registrations.
- Editor flush includes project/step selection before hotkey capture. The app hides its own windows before sampling, applies the existing 4K limit, and saves the PNG plus attachment together. Recording screenshots enter the same bounded worker queue, even during pause; hook callbacks remain unchanged. Ordinary screenshot replacement supports undo.
- Persistent dark/light themes, themed native title bar/recording toolbar, refreshed workspace and compact Export & copy menu. Removed disclaimer banner and all application message-box confirmations; errors appear inline, including a native startup error view when WebView2 cannot initialize. Native file dialogs and overwrite protection remain.
- Apply all blur/redaction stages each affected image once, updates all shared references, and commits guide/cleanup intent once before deleting originals. Render/save failure rolls back new files; cleanup failures return the committed guide and block exports until retried. The editor offers one-click application and continuation for exports and clipboard actions.

## Validation in this Linux session
- .NET 10.0.403 solution Release build passes with zero warnings/errors; Windows test sources compile.
- 63 core tests and 8 editor unit tests pass; TypeScript/Vite production build passes.
- 17 browser tests pass, none skipped. Includes screenshot attachment/new steps/undo, selection flush, independent shortcuts, theme persistence, multi-image/shared-step bulk apply, export continuation, failure/cleanup retry and offline standalone HTML. Dark/light screenshot previews inspected.
- Windows release run [42651605653](https://github.com/OathPros/Capture/actions/runs/42651605653) passed all 112 tests: 63 core, 24 native Windows, 8 editor unit and 17 browser. This includes image import/rollback, shared pixel sanitization and bulk rollback. Packaging, native packaged-WebView2 preview and installed-app install/reinstall/uninstall with user-file preservation all passed.

## Unverified / next gates
- Interactive native file pickers, actual monitor capture, independent global shortcut behavior, compositor hiding, DPI/multi-monitor alignment and native theme appearance still need manual Windows validation; CI does not establish those behaviors.
- Complete the Version 5 interactive checks in MANUAL-TESTS.md. PR #2 merged as `03654ab`; [v0.5.0-preview.1](https://github.com/OathPros/Capture/releases/tag/v0.5.0-preview.1) is published with installer, portable ZIP and synthetic export examples. Download fallback links point to this published installer.

---

# Current milestone
Version 4 quality-of-life update: meaningful capture text, fitted highlights, real strip cuts, and direct clipboard exports.

## Implemented
- UIA target resolver walks at most five elements to find a meaningful interactive control. A dedicated MTA hover sampler caches pre-click labels/bounds; event handling can fall back to its own lookup. Password suppression remains. Edit.Name and editable values are never read; separately exposed static labels and conservative whitelisted identifiers can name fields.
- Generated titles use sentence-case action prose and literal bold field/control labels. Detail instructions default blank. Exact redundant legacy details are cleared and safe old generated labels inferred; distinct user instructions are preserved. Manual title text selection can add/remove bold fields.
- Highlights fit valid physical-pixel UIA rectangles, with bounded padding, frame containment and oversized/off-target rejection; fallback is a review-marked click ring. Existing captured rectangles are not retrospectively inferred from images.
- Non-destructive horizontal/vertical Cut strip removes sections and joins remaining pixels. Original-image cut intervals, merged axis segments and source/display projection keep crop/zoom/annotation coordinates consistent. Vector overlays render after image composition; privacy pixel operations render before it. Undo/reset and every export use the same layout.
- Copy HTML code renders the complete self-contained HTML document in memory. Copy steps for Word generates standard RTF with Unicode, bold field runs and embedded PNGs, plus plain-text fallback; it intentionally omits CF_HTML to avoid Word ignoring data-URL images. Native clipboard setting occurs on the UI STA after rendering succeeds. No temporary export files.
- Schema 1.2 reads 1.0/1.1 and preserves original metadata before first upgrade save. Prior screenshot tools, export privacy guards and packaged WebView2 image-preview smoke check remain.

## Verified
- GitHub Actions Windows release run 41201180951 passed: 61 core tests, 16 Windows pixel/clipboard/control tests, 8 editor unit tests and 10 browser tests (95 total), full build, packaging and the real packaged-WebView2 screenshot decoding smoke check. Release v0.4.0-preview.1 is published. The pinned toolchain/lockfile scripts also passed locally.
- Tests cover title/field cases, legacy details, negative-origin bounds, cut interval projection/validation, HTML/RTF/document formatting, actual cut pixels in DOCX/MD/HTML, native clipboard formats and a real Windows button/text field provider. The synthetic editable field has a deliberately sensitive-looking accessible name/value that must not be read into the title.
- Browser checks exercise cut/apply/undo/reset, manual field formatting, copy-before-save with no export dialog, clipboard error recovery and existing screenshot tools.
- The exact Word clipboard RTF opened and rendered in LibreOffice with its screenshot and bold field labels. Microsoft Word's paste preference can override rich formatting; use Keep Source Formatting.

## Remaining limits
Full real-world capture/UIA timing, mixed DPI, private-window detection and app-specific provider quirks remain manual validation. Missing accessibility metadata produces a review-marked fallback. Crop/cuts retain originals in editable projects; use permanent solid redaction for secrets. PDF, signing, WGC replacement and hosted integrations remain unfinished.

## Installer preview 0.4.0-preview.2 — released
- PR #1 merged; Windows main run 41833444333 passed all 95 existing tests, packaging, installed-app WebView2 screenshot decoding, installation, repeat installation and uninstall with user-file preservation.
- Published `YU-Capture-0.4.0-preview.2-Setup-x64.exe` alongside the portable ZIP. Release asset upload and SHA-256 digest were verified through GitHub.
- The per-user Windows 11 x64 installer adds Start menu/optional desktop shortcuts and Windows Settings uninstall, bundles .NET, and conditionally installs WebView2. Project data is outside the installation directory.
- The GitHub Pages download page deployed successfully at https://oathpros.github.io/Capture/ and prefers published installer assets. The static download link also points to preview.2.
- App/installer signing, automatic updates, clean-machine prerequisite installation and interactive capture remain manual/outstanding. See INSTALLER.md.
