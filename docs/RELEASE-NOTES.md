# YU Capture 0.5.0-preview.1 — Add images to steps (unreleased)

- **Add image step** inserts a local image after the selected step, including in an empty guide.
- **Add image** attaches a picture to an existing text step. **Replace image** keeps titles, instructions and notes and resets the previous image markup/crop/cuts. **Remove image** returns the step to text only.
- Undo/Redo restores attachments and previous markup. Images work with the existing annotation editor, autosave, clipboard, document exports and portable packages.
- PNG, JPEG and BMP inputs are normalized to managed PNGs without source metadata. Limits: 64 MB per input, 8,294,400 pixels and 32,768 pixels per side. Images stay on the computer; the original external file is unchanged.
- No guide schema change. Windows chooser, decoding and installer checks remain pending; this version has not been published.

---

# YU Capture 0.4.0-preview.2 — Windows installer

Download `YU-Capture-0.4.0-preview.2-Setup-x64.exe`, open it, and choose Install. Launch YU Capture from the final setup screen or Windows Start menu. No ZIP extraction or separate .NET installation is required.

- Installs for the current Windows account, with a Start menu shortcut and optional desktop shortcut.
- Installs Microsoft Edge WebView2 if missing. This prerequisite step requires internet access.
- Adds uninstall support in Windows Settings. Saved projects under `%LOCALAPPDATA%\Capture` are preserved.
- To update, exit Capture from the tray and run the new installer.
- Requires Windows 11 x64. This preview is unsigned; follow your organization's software approval process.
- Windows CI checks installation, repeated installation, launch of the installed app with real WebView2 screenshot decoding, and uninstall with user-file preservation. Clean-machine prerequisite installation and interactive capture still need manual validation.

The portable `Capture-0.4.0-preview.2-win-x64.zip` remains available for advanced users. It requires manual extraction and WebView2 installation if missing.

The application features and schema are unchanged from 0.4.0-preview.1. Older app versions cannot read schema 1.2 guides. No automatic updater is included.

---

# CAPTURE 0.4.0 — Capture quality and clipboard

Download `Capture-0.4.0-preview.1-win-x64.zip`, extract the full ZIP, exit the older app from its tray icon, and run Capture.App.exe. Requires Windows 11 x64 and WebView2 Runtime; .NET is bundled. This is still a portable, unsigned preview.

1. **Better titles, less repetition.** Generated titles use detected control/field labels in bold, preserve their casing, and use sentence case for surrounding prose. Generated instructions stay blank unless you add useful detail. Duplicate legacy title/detail text is cleared; distinct custom instructions are retained. Existing titles can be formatted with Bold field / Clear bold.
2. **Control-sized automatic highlights.** Capture prefers a pre-click hover snapshot and the nearest interactive control's actual UIA rectangle. Invalid/oversized/off-target bounds fall back to a small click ring for review. This affects new recordings; existing highlights remain editable. Unnamed controls still need review instead of fabricated descriptions. Typed field values and Edit.Name are not read; static labels or conservative known field identifiers are used.
3. **Real Cut strip tool.** Remove horizontal or vertical sections—including browser chrome or interior gaps—and join the remaining image. Supports multiple cuts, undo/reset, zoom/crop and annotations; the result is included in file exports and clipboard screenshots. Cut/crop are reversible, not privacy redaction. Portable projects retain their source images.
4. **Copy without saving files.** Copy HTML code gives a complete self-contained document as source text. Copy steps for Word gives rich text with bold field names and embedded screenshots; paste using Keep Source Formatting. The existing DOCX/Markdown/HTML/portable file exports remain available. No temporary export files are created by either clipboard action.

Guides now save as schema 1.2 with original schema 1.0/1.1 metadata backups before first upgrade. Older application versions cannot read upgraded guides. Blur/redaction must still be permanently applied, and privacy cleanup completed, before copying or exporting. Existing external copies and backups are not erased.

Automated checks include document structure/rendering, cut seams, Windows clipboard formats, actual Windows controls, and the packaged WebView2 image preview. Interactive capture/timing/mixed DPI still need manual validation. PDF, manual image insertion, signing and hosted integrations remain unfinished.
