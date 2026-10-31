# Current milestone
Screenshot display repair and an interactive screenshot editor, following the user's real Windows feedback.

## Implemented
- Screenshot access uses an origin-checked native asset.read command constrained to the open project; validates PNG size/dimensions/checksum. Embedded data URLs avoid the failing cross-origin virtual-folder route. Loading/missing/decode errors and retry are visible.
- Canvas tools: select/move/eight-handle resize, crop/reset, blur, opaque redaction, text, lines/arrows (including reverse direction), callouts, rectangle/ellipse/highlight. Cut/copy/paste/delete, fit/zoom, undo/redo, and exact geometry. Fill/outline/text colours, stroke width, font size and shadows.
- Normalized original-image coordinates keep annotations aligned through zoom and crop. Reversible edits are persisted in schema 1.1; old 1.0 metadata is backed up before first upgrade save.
- Native GDI renderer handles vector/text properties, pixel blur/redaction and crop for Word/Markdown/HTML. All formats, including portable packages, block pending pixel edits and unfinished privacy cleanup.
- Permanent apply creates a sanitized asset, commits shared-step metadata plus cleanup intent, removes the old source and clears history. Failed save retains the original; failed cleanup can resume without losing the sanitized guide. Existing external copies are not erased.
- Screenshot-first layout, sticky step sidebar, on-canvas toolbar, meaningful error/retry and source-size display.
- New packaged-app smoke mode generates an isolated synthetic project and reports success only after WebView2 has loaded the screenshot through asset.read and the actual SVG image has decoded. Windows CI installs a signature-verified official WebView2 runtime if missing.

## Validation so far
- Core: 42 passed (including legacy migration, styling/crop validation and export privacy guards).
- Editor unit: 5 passed. Browser: 7 passed (asset load/ready, actual pointer gestures, styling, clipboard/undo, crop/reset/reopen, errors/retry, permanent apply, existing document exports and offline HTML).
- Full Windows-targeted solution compiles locally with zero warnings/errors.
- Windows rendering/transaction and actual packaged-WebView2 preview checks are configured in CI; their new results must be verified before publishing readiness claims.

## Remaining limits
Interactive recording/hotkeys/UIA/mixed-DPI acceptance remains a separate manual gate. Blur uses a soft preview filter and native box blur; it is not a confidentiality guarantee. Solid redaction plus permanent apply is the privacy path. Crop is not redaction. No forensic erasure/external-backup removal is promised. Windows Graphics Capture replacement, manual image/region insertion, PDF, SQLite, richer migrations, signing and hosted integrations remain incomplete.
