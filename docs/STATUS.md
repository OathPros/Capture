# Current milestone
M1 — first Windows capture-to-editor preview. Interactive acceptance is **UNVERIFIED**. M0 compilation/editor checks passed; native WebView2 launch remains unverified from this Linux host.

## Implemented
- .NET 10 WPF executable, bundled React/TypeScript editor, fixed-origin typed WebView2 bridge and tray lifetime.
- Configurable Ctrl+Shift+letter shortcut, consent, recording toolbar, pause/resume/finish/cancel and process exclusions.
- Dedicated minimal mouse-hook thread and bounded queue, UIA3 MTA worker, limited metadata/password suppression and click-marker fallback.
- Two-frame foreground-monitor pre-action PNG buffer, 4K limit, negative-origin mapping, toolbar capture exclusion plus rectangle masking, stale/missing frame warnings.
- Atomic JSON project persistence after each step, draft listing/recovery, validated package import with checksums/path/size/PNG checks.
- Text/step editing, duplicate/delete/reorder, bounded undo/redo, debounced serialized autosave and save flush before recording/exit. Editable highlights; Markdown rasterized-image ZIP and editable portable package exports.
- Frozen dependency lockfiles, Windows build/test/package scripts and GitHub Actions ZIP/prerelease workflow.

## Verified in this Linux task
- SDK 10.0.403 installed; real solution cross-build: **0 warnings, 0 errors**.
- Core .NET tests: **19 passed, 0 failed, 0 skipped** (schema, session transitions, negative coordinates, atomic replacement/reopen, package roundtrip, malicious paths/images, Markdown escaping).
- TypeScript check and Vite production bundle: passed.
- Vitest: **2 passed** (reorder identity/order and bounds).
- Playwright with system Chromium: **2 passed** (mocked host create/edit/save/reorder/undo/export/reopen; browser without native host).
- Self-contained Windows x64 `dotnet publish`: passed, executable and bundled editor produced.
- Native UI, capture and exported screenshots have **not** been run or visually validated on Windows. Browser tests use a mock host, not Win32.

## Blocked / unverified
- Interactive Windows 11 hotkeys, hooks, UIA, GDI timing, foreground/multi-monitor/mixed DPI, privacy behavior, clean startup/exit and packaged WebView2 loading. Follow docs/MANUAL-TESTS.md. Do not call M1 or full V1 accepted before this gate.
- GitHub-hosted CI and release publication are separate from local validation; inspect the actual run before treating a release as available.

## Not started / incomplete V1 requirements
Windows Graphics Capture replacement, richer schema/migrations, SQLite index, hard UIA process isolation/timeouts, private-window detection, manual image/region capture, crop/redaction, richer annotation tools, DOCX/PDF and shared print renderer, accessibility/performance/manual validation, signed distribution, broader export regression fixtures. V2/V3 hosted/Halo work is intentionally absent.

## Next concrete tasks
1. Run the Windows build/download and interactive manual matrix using synthetic data; record actual results and fix any native failures.
2. Complete reliable capture and timing acceptance before expanding the editor/export surface.
3. Implement remaining V1 document/image/export features and tests, then complete clean-Windows packaging/privacy/security review. This preview is not the completed product specification.
