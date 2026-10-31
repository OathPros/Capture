# Decisions

## ADR-001 — First runnable preview boundary
- Date: 2026-10-31
- Choice: Implement the M0/M1 desktop path and a basic editor plus Markdown/portable exports. Keep later features absent and explicitly documented rather than presenting inert actions.
- Why: The repository initially contained only the product specification. A buildable capture-to-editor path is the first risk to address.
- Limitations: Full V1 is incomplete; Windows interactive acceptance cannot execute on this Linux host.

## ADR-002 — Bounded GDI monitor frames for prototype
- Choice: GDI CopyFromScreen on the foreground monitor, two recent PNG frames at roughly 150 ms plus capture/encoding cost, maximum 4K. Timestamp after screen copy; select a fully captured pre-click frame no older than one second. Mask toolbar geometry as defense in depth.
- Why: Explicitly permitted prototype fallback in the guide. No WGC claim. Missing frames skip the click with a visible warning.
- Limitations: PNG compression/GDI may be slow; current-monitor sampling can miss immediate monitor switches. All generated steps need review. Actual display performance and privacy behavior require manual tests.

## ADR-003 — Recoverable folder storage before SQLite
- Choice: Atomic guide.json replacement and referenced PNGs; discover projects from GUID folders. Portable imports get a new project ID and verify checksums, archive paths/count/size and PNG dimensions.
- Why: Folder data is already specified as sufficient to reconstruct projects; no indexing service is needed for the first preview.
- Limitations: SQLite/preferences migrations, large-library performance and richer schema fields remain later work. Unknown fields and unsupported schemas are rejected instead of silently discarded.

## ADR-004 — Safe accessibility and host boundaries
- Choice: FlaUI UIA3 on a dedicated MTA thread, bounded hook queue, no editable values, password suppression, process exclusions and generic click-marker fallback. Fixed WebView2 local origins, no external navigation/popups/downloads, command allowlist and document validation.
- Limitations: COM calls lack trustworthy cancellation. Finish waits ten seconds, preserves saved drafts and requires restart on timeout. Process exclusions cover foreground/clicked app, not all visible windows. Private-window detection and redaction are absent; use synthetic data.

## ADR-005 — Small React editor before richer document tools
- Choice: Plain text fields and native numeric annotation controls; React 19/TypeScript/Vite. No raw HTML or remote content. Bounded session undo history, serialized/debounced saves, and host-requested save flush before recording/exit.
- Why: Avoid unnecessary rich-text/canvas dependencies for the first working slice. TipTap/Konva can follow the Windows acceptance gate.
- Limitations: No crop, rich text, redaction or full print renderer yet. Undo is current-session only.

## ADR-006 — Explicitly requested document exports
- Date: 2026-10-31
- Choice: Implement DOCX and improve Markdown at the user's request, then add their requested styled HTML format before full interactive capture acceptance. This is an explicit scope update; it does not mark capture gates complete.
- Design: Open XML SDK for genuine DOCX; one shared, checksummed Windows PNG annotation renderer for DOCX/MD/HTML. Format projections stay in the cross-platform core and accept a renderer callback, processing one step at a time. The native host snapshots the guide, runs export off the UI thread and atomically replaces a user-selected destination after success.
- HTML: One offline file with encoded plain text, base64 PNGs, no external dependencies, contents links, responsive/print styles and CSP. No scripts or dynamic document content.
- Validation: OpenXmlValidator, Markdown/HTML parsers, portrait/landscape/12-step fixtures, text-only/missing-image tests, browser command and offline layout checks, actual Windows GDI pixel/immutability tests in CI. LibreOffice rendering provides additional document compatibility evidence; interactive Word and native dialogs remain manual checks.
