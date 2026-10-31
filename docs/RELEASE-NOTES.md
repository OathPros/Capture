# CAPTURE 0.3.0 — Screenshot editor

Download `Capture-0.3.0-preview.1-win-x64.zip`, extract the entire ZIP, and run Capture.App.exe. Exit older Capture instances from the tray first. Windows 11 x64 and WebView2 Runtime are required; .NET is bundled.

- Fixes broken screenshot display by reading known project PNGs through the native host, with checksum/dimension checks, a visible missing-image error and Retry.
- Screenshot-first canvas: Select, Crop, Blur, Redact, Text, Line, Arrow, Callout, Rectangle, Ellipse and Highlight. Drag to draw; move and resize selected objects with handles. Cut/copy/paste/delete, zoom, undo/redo and exact geometry controls.
- Editable fill and outline colours, outline width, text colour/font size and shadows. Crop/reset is reversible. DOCX, Markdown and HTML export the edited shapes, text and crop as image pixels.
- Explicit permanent blur/redaction application replaces the shared screenshot source, clears history and removes the old managed image. All exports are blocked while pixel edits or privacy cleanup remain pending. Use solid redaction for secrets; blur is only visual softening. External copies/backups and forensic disk remnants are not erased.
- Reads legacy schema 1.0 guides; saves schema 1.1 with an original metadata backup. Older releases cannot read upgraded guides.
- Adds Windows GDI pixel/transaction tests, canvas interaction tests, and a real packaged-WebView2 screenshot decoding smoke test using only synthetic data.

Word DOCX, Markdown ZIP, styled self-contained HTML and portable package import/export remain available. `Capture-export-examples.zip` contains synthetic document samples.

This is an unsigned preview. Interactive capture/hotkeys/UIA/mixed DPI still need manual validation. PDF, manual image/region insertion, signing and hosted integrations are not included. Review screenshots before sharing.
