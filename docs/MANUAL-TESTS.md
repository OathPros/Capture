# Interactive Windows acceptance — NOT RUN

Use Windows 11 x64, WebView2 Runtime and fictional data. Record OS/build, display topology/DPI, app commit and results. Linux compilation and browser-host mocks do not establish these outcomes.

1. Extract a clean ZIP; launch Capture.App.exe without Node/.NET SDK installed. Confirm Projects loads from bundled assets, settings roundtrip and tray Exit releases the process. Try missing WebView2 and shortcut conflict cases separately.
2. Start with Ctrl+Shift+R in Notepad; consent; click menu controls; switch to a browser showing a synthetic local page; stop via the same hotkey. Confirm mouse input was never blocked/replayed, toolbar was masked/excluded, guide opens automatically, screenshots show the pre-click state and markers match the clicked locations.
3. Pause; perform clicks in another app; resume and click again. Verify paused interactions create no steps. Cancel a second recording and confirm its project directory is removed after confirmation.
4. Test on 100/125/150/200% scaling, negative-origin second display, and moving windows between monitors. Confirm off-monitor/too-stale clicks are skipped with status, not fabricated. Verify a 4K monitor and oversized-monitor warning.
5. Edit title/instructions/highlight; reorder/duplicate/delete and undo/redo; save; tray Exit; reopen and confirm changes. Exit immediately after typing to test save flushing. Kill the process during capture and reopen its saved draft in Projects. Check low-disk/permissions failure retains the previous saved guide.
6. Export Word DOCX and open it in Word or LibreOffice: verify headings, notes, page numbers, correct image order/aspect, and highlights; try long and text-only guides. Export HTML, move the single file to another folder, open it offline at desktop/mobile sizes and inspect print preview. Export Markdown ZIP; open README with relative images and rasterized highlights. Export/import a portable package; check text, step IDs, images and geometry survive. Confirm overwrite asks and cancel preserves old export. Test a malformed/traversal package produces an error without outside writes.
7. Try an excluded synthetic app and password input using dummy content. Confirm no typed values/raw keyboard events are collected. Review all surrounding screenshots: password suppression cannot guarantee absence elsewhere on screen. Verify paused capture clears frame buffers.
8. Run ten start/stop cycles and 100 clicks; observe responsiveness and bounded memory. Try inaccessible UIA, protected/elevated windows and a stalled provider; verify safe fallback/warning and restart instruction. Do not attempt to bypass protected surfaces.
9. Keyboard-navigate editor controls, review focus visibility, text scaling and high-contrast behavior. Verify export instructions and screenshots visually in a compatible Markdown viewer.

PDF and hosted integrations are not implemented; do not mark those specification gates passed.

## Screenshot editor acceptance
1. Open an existing recorded guide in the new build. Confirm PNGs are visible, step switching works and missing files produce a specific error/Retry.
2. Draw each tool; move/resize, change fill/outline/text colour, stroke/font size and shadow. Cut/copy/paste/delete, undo/redo, zoom, crop/reset and reopen. Check alignment at mixed DPI.
3. Export DOCX/MD/HTML; verify styles, text and cropped screenshots. Crop is not redaction and portable source images can be uncropped.
4. With synthetic sensitive patterns only, add blur/redaction; verify export is blocked until permanent apply. Confirm shared steps update, Undo clears and the old managed image is removed. Verify portable packages contain only sanitized referenced assets. Simulate failed saves/cleanup and retry. Existing external backups are not erased.
5. Windows CI separately runs synthetic GDI pixel tests and a packaged-WebView2 PNG decoding smoke test. Those do not prove real desktop capture timing or privacy behavior.

## Version 4 quality-of-life checks
1. Make a new recording with named buttons, tabs and labeled fields. Confirm title prose is sentence case, only field/control labels are bold, instruction details do not repeat the title, and typed values never appear. Test a custom/unnamed control and verify the honest review fallback.
2. Check that the fitted highlight encloses the actual control, including clicking a text child inside a button. Test pre-click navigation, negative-origin monitors and mixed DPI. Old recorded highlights remain editable but cannot be automatically re-detected from their PNGs.
3. Remove a browser tab/tool strip with horizontal Cut strip, then an interior gap and a vertical strip. Verify the gap closes, annotations can still be moved/resized, undo/reset/reopen works, and DOCX/MD/HTML/clipboard pictures match. Crop/cut are not redaction.
4. Copy HTML code into a text editor and open the pasted complete document offline. Copy steps for Word directly into Word using Keep Source Formatting: confirm headings, bold field runs, notes, image order/size and Unicode. Keep Text Only is expected to omit images. Verify no export save dialog/file is needed. Try a busy clipboard and verify edits remain intact.


## Version 5 image insertion — Windows validation pending
1. Create an empty guide and choose Add image step. Import a local PNG, JPEG and BMP. Verify the native chooser, local preview and insertion after the selected step. Cancel the chooser and check no step was added.
2. Add image to an existing text step, then replace a captured image. Confirm text stays intact, previous annotations/crop/cuts clear, the step needs review, and Undo/Redo restores the correct image and markup. Remove image, undo, save, exit and reopen.
3. Delete/move the original external file after import; reopen the guide and verify the managed image still loads. Annotate/crop/redact it and check Word/HTML/Markdown/clipboard and portable roundtrip.
4. Choose invalid, oversized and unavailable files. Check errors preserve text and undo history. Simulate disk/save failure, retry Save and verify the last saved guide remains usable. Removing/replacing an image is reversible editing, not secure deletion of the old project asset.
5. Run the Windows image import tests and installer/packaged WebView2 smoke checks before publishing 0.5.0-preview.1. Linux compilation/browser tests do not validate the native Windows chooser or GDI decoding.
