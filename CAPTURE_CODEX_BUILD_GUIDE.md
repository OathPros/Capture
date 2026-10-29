# CAPTURE — Codex Build Guide

**Working name:** CAPTURE  
**Product:** Local-first Windows application for automatically generating editable, illustrated, step-by-step instructions from user interactions, with export and optional institutional publishing.  
**Intended environment:** York University, initially a single-user internal prototype.  
**Specification version:** 1.0  
**Prepared:** 2026-10-28  
**Primary implementation language:** C# / .NET 10  
**Editor:** React + TypeScript embedded in WebView2  
**First delivery target:** A Windows executable that captures workflows across applications and produces an editable guide without any server dependency.

> **Instructions to Codex:** Treat this file as the product requirements, implementation plan, and definition of done. Build functioning code, not an architectural mock-up. Work through the numbered milestones in order. Do not skip tests or falsely claim Windows interactions were verified when a real interactive Windows session was unavailable. Make pragmatic implementation decisions when details are unspecified; document them in `docs/DECISIONS.md`. Do not wait for Halo credentials or York infrastructure: those are optional integration milestones with local mock adapters. Follow the privacy constraints throughout, not as a retrofit.

---

## 0. Executive directive

### The application in one sentence

Press a configurable Windows hotkey, perform a workflow across one or more desktop applications, press the hotkey to stop, and immediately open an editor containing a usable, numbered guide with screenshots, highlighted click targets, and editable instructions.

### The standard of quality

The experience should resemble the speed of ShareX and the documentation output of Scribe, but remain **native to Windows, local-first, exportable, and extensible**. It is not a browser extension and should not be dependent on AI, a browser tab, a website account, or a remote service for core functionality.

### First release priorities — in strict order

1. **Reliably capture desktop interactions** without slowing or interfering with other applications.
2. **Generate understandable steps** with correct screenshots and correctly positioned annotations.
3. **Immediately open a polished editing interface** after recording finishes.
4. **Persist editable local guides** that survive application restart and unexpected crashes.
5. **Export portable guides** to Markdown, DOCX, PDF, and JSON bundle.
6. **Support explicit, secure publication** to a web endpoint, followed by future Halo integration.

Do not substitute a demonstration UI for any of the first five outcomes. No real-time video recording, AI narration, team collaboration, browser extension, or hosted library is required for the first functioning milestone.

### Operational definitions

- **Recording session:** An explicitly started period during which the application observes interaction events and briefly buffers frames. It is *not* a continuous video file.
- **Interaction:** Mouse click, and later selected non-sensitive keyboard/focus events, associated with a foreground application and optional accessible UI element.
- **Captured step:** An event that becomes an instruction plus a screenshot and annotation. Not every raw input event must become a step.
- **Guide:** User-editable document with title, introduction, ordered steps, images, annotations, metadata, and export state.
- **Annotation:** Geometric markup rendered on or exported into a screenshot (highlight rectangle, ring, arrow, numbered marker, blur/redaction).
- **Publish:** An explicit user action to create/update a hosted rendition; never synonymous with saving a local draft.

---

## 1. Scope and release boundaries

### V1 / local MVP — required

- Windows 11 x64 initially; design for future x64/ARM64 support. Windows 10 LTSC compatibility is a stretch goal, not an assumed guarantee.
- System tray application, visible recording state, and a global configurable hotkey; default suggestion `Ctrl+Shift+R`, with a clear conflict warning if unavailable.
- Start, pause, resume, stop, cancel, and restart a recording.
- Detect primary mouse clicks across ordinary desktop applications and browsers; exclude CAPTURE's own toolbar/editor.
- Correlate click position with foreground window/process; attempt to resolve the UI element using Windows UI Automation (UIA).
- Capture useful **pre-action** imagery, with post-action fallback when needed. Correctly crop to selected window or include relevant surrounding context.
- Highlight the target button/control using a non-destructive annotation model. Fall back to a click ring if the element cannot be identified.
- Generate deterministic draft instructions from accessible element type/name and action, without capturing text typed by users.
- Automatic step sequencing and a minimal way to merge/delete noise.
- Open the guide editor automatically when the session stops.
- Edit title, introduction, step instruction, step order, and screenshot annotations; add/delete/duplicate steps; crop images; add manual screenshots.
- Save/reopen local projects with relative assets; autosave edits and recover sessions.
- Markdown-with-assets ZIP, DOCX, PDF, and structured JSON bundle exports.
- Sensible default hotkeys, a Settings panel, a Projects list, and a recording status display.
- No required sign-in and no network upload during capture or export.

### V1.1 / polish — after the V1 gates

- Optional hotkeys for pause/resume, instant screenshot/manual step, and show/hide toolbar.
- Better grouping of double-clicks, menu navigation, scrolling, and repeated interactions.
- Multi-monitor performance improvements and high-DPI edge cases.
- Cover screenshots, arrows, callouts, text overlays, and built-in annotation templates.
- Source/application detection, semantic guide templates, and review metadata.
- Optimized screenshot encoders and asset deduplication.
- Signed installer, versioned migrations, export regression suite, accessibility pass.

### V2 / hosted guide library

- Optional server-backed guide publication with York SSO/OIDC, owner/editor/viewer roles, visibility constraints, authenticated links, revocation, versioning, audit history, and object storage.
- Responsive accessible guide pages usable without the Windows app.
- Cross-device authoring through a browser version of the editor using the same document schema.

### V3 / knowledge integrations

- Halo KB **draft** publication via a tested connector with service-specific mappings, safe API authentication, uploaded image assets, and preview/dry-run.
- NEXUS relationships and publishing adapters for additional knowledge systems.
- Optional approved AI enhancement for title suggestions, step consolidation, and clarity checks — never required for basic capture.

### Explicitly out of scope for V1

- MP4 capture, audio capture, webcam, screen sharing, OCR-driven autonomous workflows, workflow replay/automation, global raw keystroke logging, password/content extraction, copying browser cookies, automated posting to external services, cross-platform builds, simultaneous collaborative editing, and public anonymous share links.

---

## 2. User journeys and UX contract

### Journey A: Record a workflow

1. User launches CAPTURE. It remains in the system tray and shows a welcome/preferences window on first run.
2. User presses `Ctrl+Shift+R` from any normal application. If the shortcut is occupied, CAPTURE presents a choice of replacements rather than silently failing.
3. Compact always-on-top toolbar appears: red recording dot, session duration, count of candidate steps, **Pause**, **Finish**, **Cancel**. The toolbar must not appear in recorded screenshots if capture exclusion can be supported; otherwise crop/mask it deterministically.
4. User clicks through a sequence in a browser, Excel, Outlook, Halo, Notepad, or another normal program.
5. Each meaningful click is queued, associated with UIA metadata when possible, and resolved against a recent pre-click frame. The app must not inject clicks or interrupt workflow.
6. User stops the session with the shortcut or Finish button.
7. CAPTURE converts the session into a guide, saves a local draft, and **opens the editor immediately**. Do not block opening on optional metadata enrichment.
8. New guide starts with a suggested title and the captured steps; all text is editable.

### Journey B: Correct and improve the guide

- Select any step to edit its title/instruction, image, or annotations.
- Drag to reorder; delete redundant steps; insert a manual text or image step; duplicate or merge adjacent steps.
- In image edit mode: adjust highlight bounding box, crop, zoom, pan, add arrow/shape/text/number, blur/redact. Keep all ordinary annotations reversible; redaction is a separately confirmed destructive operation.
- See a live final-document preview. Keyboard shortcuts, Undo/Redo, Save state, and contextual tooltips are required.
- Provide guide title, short description, intended audience, and optional service/application tags.

### Journey C: Export

- Export dropdown: Markdown ZIP, Word, PDF, JSON bundle.
- File chooser defaults to user Documents, remembers the last export directory, and never silently overwrites.
- All exports have correct image order, no broken links, readable text, and screenshots fitting printable pages.
- Include export format/version in metadata, without leaking internal full file paths.

### Journey D: Publish (V2+)

- User explicitly selects Share/Publish.
- App checks sensitive-content review status, publication permission, and target.
- User selects visibility: self, selected York users/groups, internal York readers; **public** only if separately authorized.
- App uploads guide content/assets over authenticated TLS and returns a canonical URL. User can revoke, update, or unpublish.

### UI aesthetic

Modern professional documentation tool, not a developer debugger. Neutral charcoal/white surfaces; restrained York red `#E31837` for recording/brand accents in a future York-branded build, not as a universal background. Accessible contrast, readable spacing, no ornamental gradients, no giant dashboards. Proposed editor layout:

```text
┌──────────────────────────────────────────────────────────────────────────────┐
│ CAPTURE    [Guide title]    Saved ✓          Export ▾     Share (later)     │
├──────────────────────┬───────────────────────────────────────────────────────┤
│ Steps                │ Document preview / image editor                       │
│ + Add step           │                                                       │
│ 01 Open Knowledge    │ 01  Open the Knowledge Base                            │
│ 02 New Article       │ ┌───────────────────────────────────────────────────┐ │
│ 03 Enter title       │ │       screenshot + highlight, crop or markup      │ │
│ 04 Save draft        │ └───────────────────────────────────────────────────┘ │
│                      │ Select Knowledge Base in the left navigation.          │
│                      │                                                       │
├──────────────────────┴───────────────────────────────────────────────────────┤
│ [Back to projects]              [Undo] [Redo]                    4 steps     │
└──────────────────────────────────────────────────────────────────────────────┘
```

The recording toolbar is a small native WPF surface separate from the full editor window. It must not take focus when appearing, unless the user explicitly interacts with it.

---

## 3. Technology stack and rationale

**Use these defaults unless a demonstrably better implementation is necessary. Any substitution must preserve the interfaces and be recorded in `docs/DECISIONS.md`.**

| Component | Selection | Rationale |
|---|---|---|
| Native shell and interaction agent | C# 13 / .NET 10 / WPF | Stable Windows desktop programming model, tray/window integration, mature P/Invoke story. |
| Hotkeys | Win32 `RegisterHotKey`/`UnregisterHotKey` | Reliable global shortcuts with standard message loop; explicit collision reporting. |
| Mouse events | Win32 `SetWindowsHookEx` with `WH_MOUSE_LL` on dedicated hook thread | Observe clicks without a browser extension or altering target apps. Callback must do almost no work. |
| Element identification | Microsoft UI Automation 3 / COM (pragmatically via `FlaUI.UIA3` if it builds and passes integration tests) | Accessible element name/control type/bounds across Win32/WPF/browsers. |
| Capture | Windows Graphics Capture (`Windows.Graphics.Capture`) with a capture-service abstraction | Efficient window/monitor imagery and frame access. If it proves complex in the first vertical slice, a clearly labeled GDI `CopyFromScreen` fallback is acceptable **for the prototype only**. |
| UI editor | React + TypeScript + Vite | Fast iteration, easier UI polish, reusable as a hosted editor later. |
| Document editing | TipTap | Structured rich text; keep guide step fields canonical outside rich-text DOM. |
| Screenshot annotation | Konva/react-konva | Shape editing and image overlays; export raster composites when necessary. |
| Desktop bridge | Microsoft Edge WebView2 | Hosts the editor in the WPF shell; typed JSON request/response bridge. |
| Local metadata | SQLite using EF Core or Dapper | Index/recover drafts, app settings, recent projects. |
| Local files | User-scoped project directories + portable ZIP bundles | Explicit separation of structured data and image assets. |
| Word export | Open XML SDK | Native DOCX creation without requiring Word. |
| PDF export | WebView2 print-to-PDF of a dedicated print layout | Maintains one styling pipeline for preview and PDF. |
| Hosted API (later) | ASP.NET Core .NET 10 | Natural DTO sharing and institutional hosting. |
| Hosted database (later) | PostgreSQL | Durable shared guide records and version history. |
| Hosted image assets (later) | Managed object storage | Images and thumbnails separated from DB rows. |

**Important source/API notes:**

- Microsoft's .NET 10 is LTS; supported Windows desktop workloads include WPF. See https://learn.microsoft.com/dotnet/core/releases-and-support and https://learn.microsoft.com/dotnet/fundamentals/implementations .
- Use physical pixel coordinates consistently: UIA element bounds and pointer locations must be mapped to images on per-monitor-DPI-aware processes. See https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process .
- UIA work must execute on a dedicated COM MTA worker, not the WPF UI thread. See https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading .
- Avoid low-level hook stalls: https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc .
- Windows Graphics Capture desktop interop: https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow .
- WebView2 local content/origin security: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content and https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security .
- WebView2 printing: https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/print .

### Dependency discipline

Check current stable package versions during implementation, pin them in the relevant lockfiles, and commit both NuGet lockfiles (if enabled) and `package-lock.json`. Avoid prerelease packages without a documented reason. Do not invent package versions or rely on generated code that has not compiled. Check license compatibility before adding a library; prefer MIT/Apache/BSD-compatible components for an institutional application.

---

## 4. Repository layout

Build a mono-repo with intentionally separate native, document, editor, export, and server projects:

```text
capture/
├── AGENTS.md                       # Codex operating rules (create from this guide)
├── README.md                       # Human setup, running and limitations
├── CAPTURE_CODEX_BUILD_GUIDE.md    # This specification
├── .gitignore
├── .editorconfig
├── global.json                     # Pin .NET SDK once confirmed installed
├── Capture.slnx                    # Or .sln, depending on SDK tooling support
├── docs/
│   ├── DECISIONS.md
│   ├── STATUS.md
│   ├── SECURITY.md
│   ├── TEST-MATRIX.md
│   ├── API-CONTRACTS.md
│   └── MANUAL-TESTS.md
├── src/
│   ├── Capture.App/                # WPF executable and tray integration
│   ├── Capture.Core/               # Domain models and state machine
│   ├── Capture.Windows/            # Hooks, capture, UIA, coordinates
│   ├── Capture.Storage/            # SQLite + project file persistence
│   ├── Capture.Export/             # MD/DOCX/PDF/JSON exporters
│   ├── Capture.Integrations/       # Future publish abstractions / Halo stub
│   ├── Capture.Contracts/          # Shared JSON schemas or C# DTOs
│   └── capture-editor/             # React, TypeScript, TipTap, Konva, Vite
├── tests/
│   ├── Capture.Core.Tests/
│   ├── Capture.Windows.Tests/      # Coordinate/metadata tests; interactive marked separately
│   ├── Capture.Storage.Tests/
│   ├── Capture.Export.Tests/
│   ├── Capture.Integration.Tests/
│   ├── editor-e2e/                # Playwright browser-based editor tests
│   └── fixtures/                   # Synthetic screenshots and safe guide fixtures
├── scripts/
│   ├── dev.ps1
│   ├── build.ps1
│   ├── test.ps1
│   ├── package.ps1
│   └── smoke-test.ps1
├── samples/
│   ├── halo-knowledge-create/      # Fictional/sample workflow
│   ├── excel-workbook-edit/
│   └── cross-application/
└── .github/workflows/
    └── ci.yml                      # Windows build + unit tests + editor tests
```

Keep API-facing DTOs and document schema separate from UI framework-specific objects. No React components in C# projects; no Win32 calls inside the React app.

---

## 5. Native architecture and interfaces

### Process model

One normal-user Windows executable with three independent services:

1. **Control plane:** tray, hotkeys, toolbar, session transitions, app lifetime.
2. **Capture plane:** low-level mouse hook → bounded event queue → UIA worker + screenshot service → step candidate assembler.
3. **Document plane:** persistent Guide model → editor bridge → autosave/export.

The WPF UI thread must never perform UIA cross-process calls, screenshot image encoding, database writes, or export rendering synchronously.

### Required interfaces (sketch)

```csharp
public interface IHotkeyService : IDisposable
{
    bool TryRegister(HotkeyDefinition hotkey, Action callback, out string? error);
    void UnregisterAll();
}

public interface IInteractionSource : IAsyncDisposable
{
    IAsyncEnumerable<RawInteraction> ObserveAsync(CancellationToken cancellationToken);
}

public interface IElementInspector
{
    Task<ElementSnapshot?> InspectAtAsync(ScreenPoint physicalPoint,
        CancellationToken cancellationToken);
}

public interface IScreenshotSource : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
    Task<CapturedFrame?> GetPreEventFrameAsync(RawInteraction interaction,
        CancellationToken cancellationToken);
    Task<CapturedFrame?> CaptureNowAsync(CaptureTarget target,
        CancellationToken cancellationToken);
}

public interface IStepAssembler
{
    StepCandidate Build(RawInteraction interaction,
        ElementSnapshot? element, CapturedFrame? frame);
}

public interface IGuideRepository
{
    Task SaveAsync(Guide guide, CancellationToken cancellationToken);
    Task<Guide?> LoadAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<GuideSummary>> ListAsync(CancellationToken cancellationToken);
}

public interface IGuideExporter
{
    ExportFormat Format { get; }
    Task<ExportResult> ExportAsync(Guide guide, string targetPath,
        CancellationToken cancellationToken);
}

public interface IPublishingAdapter
{
    string DestinationId { get; }
    Task<PublicationPreview> PreviewAsync(Guide guide, CancellationToken token);
    Task<PublicationResult> PublishDraftAsync(Guide guide, CancellationToken token);
}
```

These are interface intent, not copy-paste guaranteed final signatures. Codex must implement coherent compileable interfaces and update them if necessary, keeping the boundaries.

### Session state machine

```text
Idle → Starting → Recording ↔ Paused → Finalizing → Editing → Idle
            ↘ Failed         ↘ Cancelled
```

Required invariants:

- Never have two active capture sessions.
- Hotkey behavior is explicit: in Idle, start; in Recording, finish; in Paused, finish (or configurable resume — avoid ambiguity in UX).
- Starting fails safely when prerequisites are missing; hook and capture resources always release.
- Cancel discards session frames and candidates after user confirmation; no accidental export or upload.
- Finalize is idempotent and flushes event queue before loading editor.
- If app crashes during recording, recover already-persisted candidate steps into a clearly labeled recovered draft.
- Editing never restarts capture automatically.
- On process exit, unhook input, unregister hotkeys, release graphics resources, and close database connections.

### IPC/bridge contract

Use a **narrow, versioned JSON messaging API** between WebView2 and the WPF host. Each request has `requestId`, `type`, `payload`, `protocolVersion`; responses have `requestId`, `ok`, `result` or sanitized `error`.

Minimal message types:

```text
editor.ready
project.open
project.save
project.rename
project.list
project.delete
step.add
step.update
step.reorder
step.delete
asset.read           # Prefer explicit safe URL mapping over arbitrary filesystem reads
asset.replace
export.request
recording.manualScreenshot
settings.get
settings.update
```

Prefer a small set of document-level commands over exposing generic native file or shell access. Validate all messages and their originating URL. Never expose arbitrary execute-command, unrestricted filesystem read, or HTTP proxy functions through the bridge. Use WebView2 virtual host mappings to serve bundled local editor assets, disallow navigation to untrusted origins, and apply a restrictive Content Security Policy.

---

## 6. Windows capture subsystem — build this before polishing UI

### 6.1 Global hotkeys

- Register with `RegisterHotKey` against a hidden WPF-owned window/message pump and handle `WM_HOTKEY`.
- Default toggle `Ctrl+Shift+R` is only a suggestion; if registration returns failure, display a human-readable shortcut conflict and allow rebinding.
- Use `MOD_NOREPEAT`; never try to hijack Windows-reserved combinations. Restore/clean up registration on restart/shutdown.
- Settings must allow start/stop and pause/resume shortcuts to be distinct.

Reference: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey

### 6.2 Mouse hook lifecycle

- Install `WH_MOUSE_LL` from a dedicated thread with a message loop.
- The hook callback must **only** timestamp/copy minimal event data into a bounded queue, then call `CallNextHookEx` immediately. **Never call UIA, disk, logging sinks, screenshot APIs, blocking locks, or network from inside the hook.**
- Capture primary left button down as preferred interaction event; optional right click/double click in later milestone. Store physical position and monotonic timestamp. Do not intercept or block the click.
- Detect and ignore injected events when practical; ensure internal CAPTURE windows are excluded.
- If queue fills, drop low-priority events and show a non-blocking warning rather than stalling the user's desktop.
- When paused, remove or disable event collection. If no session exists, the hook should not be active unless implementation needs it for startup, and then must discard input.

### 6.3 UI Automation worker

- Create a dedicated MTA worker for UIA cross-process calls, as required by Windows guidance.
- On click, resolve foreground window/process, then query the element beneath the point using UIA3 (`ElementFromPoint` equivalent).
- Collect **only safe metadata**: `name`, `automationId` if helpful, `controlType`, `boundingRectangle`, `isEnabled`, `isPassword`, containing window/process, and optionally a sanitized label from an ancestor.
- Never read `ValuePattern.Value` or actual typed text. Do not capture raw keyboard input. For password-type fields, record a generic, safe instruction or suppress the step and warn about manual review.
- Protect every cross-process lookup from stale/dead elements, access denied, null values, expensive tree walks, and timeouts. Use a shallow bounded ancestor search; no whole-desktop tree serialization.
- When UIA fails, create a valid fallback step using the click location: `Select the highlighted control.` and set `needsReview = true`.
- Application-specific handlers may later improve instruction quality but must never become required for core functionality.

### 6.4 Screenshot acquisition and timing

**Problem:** A button can navigate or disappear immediately after clicking. Capturing the screen *after* receiving the event can show the wrong page. Therefore implement a recent-frame buffer or another demonstrably reliable pre-click strategy.

Recommended implementation:

1. Maintain a small, bounded in-memory buffer of recent screenshots for active/foreground monitor(s), e.g. a rolling ~1-second window at an adaptive sampling rate. This is still-image buffering, **not video recording**.
2. At click timestamp `t`, select the latest valid frame timestamped **before** `t`, ideally with age under ~300 ms. If stale, mark the frame as uncertain and offer a post-click screenshot as fallback.
3. Asynchronously inspect the clicked control and capture its physical-pixel bounds. UIA results may arrive after navigation; retain click-coordinate fallback.
4. Crop the selected frame to foreground-window bounds (with padding) or the monitor when a window-specific crop would omit relevant dialogs/menus. Account for negative virtual-desktop coordinates.
5. Convert target rectangle from physical desktop pixels to coordinates relative to the **actual retained image crop**. Persist normalized 0–1 annotation coordinates, but retain native metadata in a local provenance section if needed for debugging.
6. Encode only selected stills to project assets; discard unselected ring-buffer frames. Limit memory and disable frames on pause/stop.

Do not allocate unlimited full-resolution frame histories. Target adaptive sampling, controlled compression and per-monitor resource caps. At 4K resolutions, test memory usage explicitly.

For an initial spike, a screenshot sampled before click with desktop capture can be acceptable even if WGC is incomplete. Document known screenshot limitations and replace fallback before treating Windows Graphics Capture as fully implemented. Never claim protected content can always be captured.

### 6.5 Multi-monitor, DPI and window bounds

- Set **PerMonitorV2 DPI awareness** in application manifest before creating windows/capture objects.
- Treat UIA element bounds and raw mouse coordinates as **physical desktop pixels**.
- Handle monitor origins such as `(-1920, 0)`; coordinates cannot be assumed positive.
- Convert between desktop pixels, captured image crop, normalized annotation coordinates, canvas CSS pixels, and print render sizes in a single tested `CoordinateTransformer` module.
- If target UI element bounds are empty, offscreen, absurdly large, or outside image crop, use click marker centered at pointer rather than drawing an incorrect large highlight.
- Handle 100%/125%/150%/200% scaling; multiple displays with different scaling; moving an application between screens; maximized and minimized windows; modal dialogs and virtual desktops.
- Exclude CAPTURE's own toolbar via display affinity/capture exclusion when feasible; otherwise detect overlay rectangle and produce a safe alternative crop. Verify actual behavior on target OS; never assume exclusion works in all capture methods.

### 6.6 Capturing meaningful steps

Keep raw event capture separate from the step-generation policy.

Initial heuristics:

- One mouse click → one candidate step if it is a valid external target.
- Collapse duplicates occurring in rapid succession on the same control only when this cannot destroy meaningful double-click semantics.
- Ignore click on CAPTURE's controls, taskbar toolbar, repeated scroll-wheel events, and obvious click noise as configurable rules.
- Use `Button` → `Select {name}`; `TabItem` → `Open the {name} tab`; `MenuItem` → `Choose {name}`; `Hyperlink` → `Open {name}`; `Edit` → `Enter the required information in {label}` (never actual value); `CheckBox` → `Select/Clear {name}` only if the actual state transition is known, otherwise `Change the {name} option`; unknown → `Select the highlighted area`.
- Prefer verbs **Select**, **Open**, **Enter**, **Choose**, **Save**, **Review** with sentence case. Escape/sanitize names before rendering Markdown/HTML.
- Preserve low-confidence steps for user review rather than pretending the generated description is certain.
- Do not claim to know the successful outcome of a click; a click on Save is not proof that content was saved.

### 6.7 Manual screenshot feature

Add a `Capture screenshot` toolbar action that captures foreground window or a user-selected region and inserts a manual image step. For true region capture, offer a selection overlay rather than silently taking the whole screen. This complements automatic action capture and gives users control over missed states.

---

## 7. Canonical document schema

The guide **JSON model is the source of truth**. Do not derive it by parsing rendered HTML, DOCX, or Markdown. Exporters are projections of this model.

### Suggested contract (`schemaVersion: "1.0.0"`)

```json
{
  "schemaVersion": "1.0.0",
  "id": "7cb42f15-71c9-4553-88d7-0c3502638de0",
  "title": "Create a knowledge article in Halo",
  "summary": "Create and save a new knowledge article as a draft.",
  "audience": ["IT agents"],
  "serviceRefs": [],
  "applicationRefs": [{"name": "Halo", "externalId": null}],
  "author": null,
  "status": "draft",
  "createdAt": "2026-10-28T14:00:00Z",
  "updatedAt": "2026-10-28T14:05:00Z",
  "reviewedAt": null,
  "tags": ["Knowledge", "KCS"],
  "steps": [
    {
      "id": "b77a72d6-2382-4fa3-8924-881676401124",
      "order": 1,
      "kind": "interaction",
      "title": "Open the Knowledge Base",
      "instruction": "Select Knowledge Base in the left navigation.",
      "notes": "",
      "assetId": "shot-001",
      "annotations": [
        {
          "id": "annotation-1",
          "kind": "highlight",
          "x": 0.072,
          "y": 0.181,
          "width": 0.215,
          "height": 0.049,
          "style": "york-red-outline"
        }
      ],
      "source": {
        "application": "Halo",
        "interactionType": "click",
        "controlType": "MenuItem",
        "elementName": "Knowledge Base",
        "confidence": "medium"
      },
      "needsReview": false
    }
  ],
  "assets": [
    {
      "id": "shot-001",
      "relativePath": "assets/shot-001.png",
      "mimeType": "image/png",
      "width": 1280,
      "height": 800,
      "sha256": "REPLACE_WITH_REAL_HASH"
    }
  ],
  "publication": null
}
```

This is an example fixture, not a claim about any real Halo data. Actual document IDs/timestamps/hashes must be generated by the application. Do not put credential values, raw key presses, full document paths, OAuth tokens, confidential UI text, or unredacted screenshots into diagnostic event data.

### Schema rules

- `schemaVersion` is mandatory and migration-aware; reject unsupported future major versions with a useful explanation, not destructive parsing.
- IDs are stable UUIDs; step `order` is computed from ordered array or validated and normalized, never allowed to duplicate.
- Asset paths are relative and validated against traversal (`..`, drive paths, network paths). Files outside the project root must not be read from arbitrary JSON imports.
- Annotations use normalized image-space coordinates and carry their own IDs; clamp or reject invalid values.
- All display text is user-editable. Use plain text fields for title/instruction and controlled rich text only where necessary.
- Include explicit confidence or `needsReview` instead of hiding automation uncertainty.
- Publication metadata is optional; never required for local import/export.
- Preserve original schema fields through import/export when possible; use schema migration functions for changes.
- Keep UIA/Windows event details local and minimal; strip diagnostic source details from published public pages unless explicitly needed.

### Filesystem layout

Use an OS-appropriate path such as `%LOCALAPPDATA%\Capture\Projects\{guideId}\`:

```text
{guideId}/
  guide.json
  assets/
    shot-001.png
    shot-002.png
    thumbnails/
      shot-001.webp
  recovery/
    session-state.json
```

Keep SQLite as an index and preferences store; make the folder `guide.json` plus assets sufficient to reconstruct a project. For portable exchange use a ZIP (suggest extension `.capture`) with `guide.json` and relative assets, validating ZIP entry sizes/count/path traversal during import. Autosave should write temp files then atomically replace where supported; images should be written before guide references are committed. Build database migrations and recover from failed writes.

---

## 8. Editor requirements

### 8.1 Pages/components

- **Projects**: recent guides, title, modified time, number of steps, status, open/duplicate/delete/import.
- **Recording status**: compact native overlay; visible indication, pause/stop/cancel, step count, health warnings.
- **Guide editor**: steps sidebar, large preview, header actions, Save indicator, Undo/Redo.
- **Image editor**: canvas stage with move/resize highlights, callout tools, crop, zoom, and safe redaction tools.
- **Export dialog**: format, filename/location, optional table of contents, page header/footer.
- **Settings**: shortcuts, auto-launch on login (off by default), capture mode, target window/monitor, screenshot quality, storage directory, exclusions, privacy settings.
- **About/diagnostics**: version, build, troubleshooting, local logs location, optional export of sanitized diagnostic report.

### 8.2 Editor interaction rules

- Selecting a step does not alter its screenshot.
- Drag/reorder updates the guide model immediately and is undoable.
- Title/instruction editing is debounced and autosaved; explicit Ctrl+S also works.
- Screenshot annotations are non-destructive geometry layers unless they are redactions.
- Redaction is destructive at export and publish; **before publication, flatten redaction into a sanitized asset and securely remove the ordinary unredacted source copies within the managed project**. Do not promise forensic secure erasure on SSDs or existing backups. Warn when older exported/published copies may persist.
- Crop should have an undoable transform (or preserve original project image) until redaction has been finalized; define precedence: redact source pixels, then crop/annotate/export. Never merely place an opaque rectangle over unredacted pixels in a downloadable HTML document.
- Announce status/errors through accessible UI, never through color only.
- Protect against accidental loss: close with dirty unsaved changes must prompt; captured guide drafts should be persisted before editor opens.

### 8.3 Rich text and sanitization

Use a safe allowed list for formatting: paragraph, heading, bold, italic, links, lists, simple code. Reject arbitrary HTML/JS in imported content. Never embed raw external web pages into the editor. Validate link schemes (`https`, `http`, optionally `mailto`); do not let generated link or user-supplied image paths execute scripts. WebView2 host bridge must never trust content from the document itself.

### 8.4 Reusable step preview

Build one accessible `GuideRenderer` used by desktop preview, print/PDF, hosted guide viewer, and export snapshot tests wherever practical. It must honor annotations/crops, image aspect ratio, natural page breaks, and text wrapping without cutting off content.

---

## 9. Exports — precise acceptance criteria

### 9.1 Markdown

**Output:** ZIP containing `README.md` and `assets/*` (or a user-chosen folder equivalent).

Example:

```markdown
# Create a knowledge article in Halo

Create and save a new knowledge article as a draft.

## 1. Open the Knowledge Base

Select **Knowledge Base** in the left navigation.

![Knowledge Base highlighted in Halo](assets/step-001.png)

## 2. Create a new article

Select **New Article**.

![New Article highlighted](assets/step-002.png)
```

Requirements:

- Rasterize ordinary annotations into **export copies** of screenshots, preserving project editability.
- Use relative links; ensure all referenced files are included.
- Render only permitted formatting; escape Markdown syntax in user strings.
- Generate useful alt text without pretending to identify UI elements that were not verified.
- When the guide has no screenshots, export still succeeds and gives instructions.

### 9.2 DOCX

**Output:** `.docx` generated without Microsoft Word installed.

- Include title, summary, optional metadata (author, audience, reviewed date), numbered step headings, instruction text, images/alt text, and optional footer/page numbers.
- Size images to available page width while preserving proportions. Prevent giant images from overflowing page width.
- Keep heading and corresponding instruction together where possible; support accessible heading hierarchy, correct document language, and readable contrast.
- No placeholder watermarks, test images, or proprietary brand assets in the output.
- Test file integrity with ZIP structure checks and automated DOCX inspection; optionally render/inspect on Windows when Office or a compatible viewer is available.

### 9.3 PDF

**Output:** `.pdf` produced from a dedicated print layout (not by screenshotting the editor screen).

- Use WebView2 `PrintToPdfAsync` or an equivalent verified print pipeline.
- Default letter-size paper for York/Canada; allow A4 selection in settings later.
- No navigation controls or editing chrome in export; add proper page margins and optional headers/footers.
- Images should not be clipped; long instructions flow between pages, avoiding orphaned headings where possible.
- Verify that the file opens and has nonzero pages; text should remain selectable.

### 9.4 JSON and portable package

**Outputs:**

1. `.json` document metadata/structure only, with documented relative assets — **not guaranteed standalone**;
2. `.capture`/`.zip` portable package with JSON and all assets — the recommended lossless transfer format.

- Validate content against JSON schema before exporting or importing.
- Provide user-friendly errors for missing image assets.
- Include checksums for asset integrity; never put credentials in exports.
- ZIP import must defend against zip slip, decompression bombs, and huge media payloads.

### Shared export verification

Create a fixture guide with 10+ steps, multiple images, portrait and landscape screenshots, long text, arrows, highlighted controls, crop and redaction. Test exports against it. Re-import the portable bundle and verify semantic equality excluding generated timestamps/export history.

---

## 10. Privacy, security, and consent — blocking requirements

This tool may be used near real institutional information. **Safe defaults are necessary even in an individual prototype.**

1. **Explicit capture only.** No hidden background recording; clear visual recording indicator; start/stop shortcut; easy cancel.
2. **No raw keylogging.** Use `RegisterHotKey` and mouse click events; do not persist arbitrary keystrokes or input values. Do not inspect password-value properties.
3. **Sensitive application exclusion list.** Support per-process/window exclusion patterns, with a default option to exclude browsers' private/in-private windows where detectable (best effort, not a promise) and password managers where identifiable. Allow user-managed exclusions.
4. **No network dependency.** No telemetry, analytics, crash dump upload, remote AI calls, or screenshot upload by default.
5. **Local storage.** Save to user-scoped application data, not shared program directories. Respect Windows account permissions; document whether assets are encrypted at rest (V1 can rely on OS-account protections but must not claim end-to-end encryption).
6. **No credential leakage in logs.** Diagnostics contain timings/error codes and anonymized process metadata only; no screenshots, text entries, raw UI tree, tokens, cookie files, or URLs with query strings.
7. **Pre-publication review.** Publishing is always deliberate and separate from save. Require user review/acknowledgment when step images contain unredacted captures.
8. **Redaction.** Image-based blur is not sufficient to remove text securely if original assets remain accessible. Implement destructive sanitization for final assets; offer solid-fill redaction or irreversible pixelation with adequate strength. Treat any OCR-based sensitive-data scan as advisory only.
9. **Protected surfaces.** Do not attempt to bypass UAC secure desktop, elevated-app access barriers, DRM, protected video, lock screens, Windows logon screens, enterprise DLP, or application policy restrictions. If capture fails, show a meaningful failure state and manual-step option.
10. **Secure imports.** No arbitrary asset paths, active content, or shell execution from `.capture`/JSON/Markdown imports.
11. **Signed distribution later.** Before enterprise pilot, code-sign installer/binaries; complete software inventory, accessibility, security, and privacy review under York processes.
12. **No arbitrary public links by default.** Hosted publishing requires authenticated authorization, revocable access, audit logs, bounded retention, and service-owner governance.

### Threat scenarios and mitigations to test

| Threat | Required control |
|---|---|
| User captures password screen | Password-aware UIA suppression, capture pause, review prompt; cannot guarantee perfect visual detection. |
| Sensitive screenshot remains behind black visual overlay | Actual raster redaction before share/export; purge managed unredacted copies from active project. |
| Untrusted guide JSON reads `C:\Users\...` | Asset path canonicalization + project-root containment checks. |
| Imported HTML triggers JavaScript in editor | Rich-text sanitization, CSP, untrusted content treated as data. |
| WebView navigation calls native APIs | Origin check on every message; reject non-bundled origins; typed command allowlist. |
| Global hook freezes target app | Microsecond-scale callback + queue + dedicated processing workers. |
| Publish action uses leaked embedded API key | User auth / server-side secrets; no hardcoded production credentials. |
| Other staff can discover private guide link | Authenticated visibility rules, authorization on guide and asset access. |

---

## 11. Hosted publishing architecture (V2 — design now, build later)

Do not delay the V1 local MVP on any backend implementation. Still maintain a clean `IPublishingAdapter` abstraction.

### Proposed deployment

```text
Desktop app / Browser guide editor
          │
          │ OIDC login + HTTPS API
          ▼
  ASP.NET Core publishing API
          ├── PostgreSQL: guide IDs, versions, ownership, ACLs, metadata
          ├── Object store: screenshots, thumbnails, sanitized exports
          ├── Hosted guide renderer: responsive accessible pages
          └── Adapters: Halo KB / future platforms
```

### Identity and authorization

- York SSO/OIDC only after institutional technical approval and client registration.
- `owner`, `editor`, `viewer`, `publisher`, `admin` roles with resource-level authorization, not just UI-hiding.
- Guide visibility: `private`, `selected-users`, `york-authenticated`, optionally `public` behind admin policy. Default `private`.
- Access check applies equally to JSON, image assets, thumbnails, exports, and history.
- Use expiring access URLs only for assets where appropriate; server-mediated access preferred for protected materials.
- Maintain audit events for publish, view (if policy permits), changes, role grants, revoke, and deletion.

### Suggested HTTP API (logical contract; refine before implementation)

```http
POST   /api/v1/guides
GET    /api/v1/guides/{id}
PATCH  /api/v1/guides/{id}
POST   /api/v1/guides/{id}/assets
POST   /api/v1/guides/{id}/publish
POST   /api/v1/guides/{id}/unpublish
GET    /api/v1/guides/{id}/versions
GET    /api/v1/guides/{id}/versions/{version}
POST   /api/v1/guides/{id}/permissions
GET    /g/{slug}              # Hosted viewer, authorized by policy
```

- Include ETags/revision tokens to avoid accidental overwrite.
- Upload requests are authenticated and use bounded content-length/MIME checks and server-side image validation.
- Publish endpoint is idempotent and validates review/permissions. Server returns canonical URL, published version, and visibility.
- Public viewers never receive original unredacted images or internal click/automation metadata.

### Versioning

Every successful publication produces an immutable published version with a distinct timestamp and author, while the editable draft continues independently. Unpublish revokes URL access, but retention/backups must follow a documented policy; do not promise absolute deletion of previously downloaded documents.

---

## 12. Halo Knowledge Base integration (V3)

**Do not fabricate York-specific Halo endpoints, article schemas, credentials, or workflow status values.** Halo's API uses OAuth2 and JSON, and instance configuration should be confirmed through York's own API documentation/Swagger. Halo describes its platform API at https://haloitsm.com/apidoc/ and documents the instance-specific API at `<Halo Agent Portal URL>/api/swagger` in https://haloitsm.com/guides/article/?kbid=1823 . The general documentation available publicly may be older than York's installed release.

### Integration behavior

1. User chooses **Publish to Halo KB**.
2. CAPTURE checks destination profile and authorization, and shows preview of article fields.
3. User maps/overrides title, article type, description, resolution, internal memo where appropriate, tags, audience, knowledge base/FAQ location, and visibility settings.
4. App renders the guide into Halo-compatible article body markup, with sanitized screenshots hosted or uploaded through supported image/attachment APIs.
5. **Create a draft** or non-public article with known safe default permissions. No automatic publication.
6. Return created article ID and canonical URL; store the mapping on the guide.
7. Future updates show diff and conflict warning; never silently overwrite edits made inside Halo.

### Expected data mapping (illustrative, verify against York instance)

| CAPTURE field | Halo concept | Notes |
|---|---|---|
| Guide title | Article Title | Human review required. |
| Summary/context | Description | Separate problem/context from resolution. |
| Ordered steps + images | Resolution | Render as readable HTML compatible with Halo editor. |
| Author notes | Internal Memo | Only with explicit toggle, never publish internal notes by default. |
| Tags | Tags / metadata | Normalize to configured vocabularies. |
| Audience/team | Allowed Customers / Teams access / User roles | Never infer permissions blindly. |
| Review metadata | Next Review Date / status | Align with existing governance. |
| Linked service/operation | External mapping / tags | Keep source IDs separate. |

### KCS alignment

Do not assume every recorded process is a publishable knowledge article. The editor should support additional context fields: **issue/request**, **environment**, **resolution**, **validation**, **audience**, **reuse notes**, and **review date**. Keep these optional in V1 and map them into appropriate Halo fields later. Generated steps require author validation; recording a click is not proof that a resolution worked.

### Implementation pattern

- Create `HaloKnowledgePublisher` behind `IPublishingAdapter`.
- Start with `MockHaloPublisher` implementing preview, sample article response, and error scenarios. Use fictional sample data clearly marked in test code and fixtures.
- Introduce actual OAuth2 only after approved credentials and known authentication flow are provided. A desktop public client must not contain a long-lived confidential client secret; use an approved interactive flow or a secure server-side broker as determined by York's identity/security team.
- Parse documented Swagger/OpenAPI or confirmed request/response samples before implementing endpoints. Save the verified mapping and version in `docs/HALO-INTEGRATION.md`.
- Support timeouts, safe retries with idempotency where available, rate limits, asset upload failures, and rollback/cleanup where the remote API permits.

---

## 13. NEXUS and service ontology interoperability (future, not a V1 dependency)

Keep the guide model able to reference governed knowledge relationships without turning application/product names into service records.

Suggested optional references:

```json
{
  "serviceRef": {"nexusId": "svc-email-calendaring", "label": "Email & Calendaring"},
  "offeringRef": {"nexusId": "off-faculty-staff-email", "label": "Faculty & Staff Email"},
  "operationRef": {"nexusId": "op-create-shared-mailbox", "label": "Create shared mailbox"},
  "applicationRef": {"nexusId": "app-microsoft-365", "label": "Microsoft 365"},
  "knowledgeType": "procedure"
}
```

Identifiers above are illustrative **only**, not authoritative York records. Schema must permit absent refs and preserve raw external IDs without making NEXUS a required runtime service.

Possible future linkage:

```text
Service → Offering → Operation → HAS_KNOWLEDGE → Guide / Halo article
                                        │
                                        └→ USED_BY / FOR_AUDIENCE
```

Do not implement NEXUS graph storage or direct database writes inside CAPTURE. Use future validated API contracts.

---

## 14. Detailed implementation milestones (Codex must follow in order)

### M0 — Bootstrap and foundations

**Deliverables**

- Working repository, `.gitignore`, license decision placeholder, C# solution, React editor project, tests, scripts, README, `AGENTS.md`, CI stub.
- `Capture.Core` schema definitions, JSON serialization, guide test fixtures, schema validator, first migration stub.
- Windows executable opens an empty shell with projects view in WebView2, and can close cleanly.
- Typed WebView2 bridge proof: `editor.ready` and `settings.get` roundtrip.

**Acceptance gate**

- `dotnet build` succeeds on Windows with no unresolved projects.
- `npm ci && npm run build` succeeds for React.
- Native app launches bundled editor without a dev server.
- Console/build has no warnings hidden by blanket suppression.

### M1 — Recording vertical slice (highest priority)

**Deliverables**

- Tray icon, hotkey registration/conflict response, dedicated low-level mouse hook, bounded event channel, session state machine.
- UIA worker using UIA3 with safe metadata extraction and fallback.
- Screenshot capture service with proper physical-coordinate mapping and temporary image persistence.
- Assemble a draft step per click with generated sentence and screenshot + annotation.
- Finish session → guide saved → editor opens automatically.

**Acceptance gate: real Windows manual test**

1. Open Notepad; start recording via hotkey.
2. Click menu/toolbar controls and navigate between at least two normal windows.
3. Stop with shortcut.
4. Editor shows numbered steps, screenshots and correctly positioned click markers/highlights.
5. None of the clicks were blocked, repeated, or injected; no major app lag.
6. Unidentifiable controls become editable fallback steps instead of errors.
7. If automated capture is unavailable in the Codex execution environment, complete compilation and deterministic tests, mark this gate **UNVERIFIED**, and provide exact user-run smoke-test instructions. Do not claim pass.

### M2 — Reliable image timing and cross-app capture

**Deliverables**

- Bounded recent-frame buffer with pre-action screenshot selection, monitor-awareness, crop and animation/dialog timing handling.
- Optional foreground window crop, manual screenshot step, camera health errors, excluded-app handling.
- Multi-monitor and mixed-DPI test cases.
- Captured screenshot annotation coordinates remain correct after crop/export.

**Acceptance gate**

- For fast navigation (click leading to immediate page change), image usually shows the control **before** the click; stale/missing frame is detected and review-marked.
- Clicks on secondary display and on negative-coordinate monitor map to appropriate image position.
- Screenshot service uses bounded memory over a 10-minute representative session; no unbounded image/frame accumulation.

### M3 — Real guide editor

**Deliverables**

- Projects, guide layout, drag reorder, step editing, image canvas, highlight editing, crop, arrow/marker/redaction, Undo/Redo, Save, autosave, recovery.
- Typed bridge commands and safe asset loading.
- Settings and simple exclusion manager.

**Acceptance gate**

- Add, edit, reorder, delete and restore steps; restart and reopen same project without loss.
- Annotations scale correctly at 100% and 200% zoom.
- Redacted output genuinely removes original visual content from exported assets.
- Keyboard-only basic navigation through steps and editor controls is possible; focus visible.

### M4 — Export suite

**Deliverables**

- Working Markdown ZIP, DOCX, PDF, JSON and portable `.capture` exports; UI actions wired to each implementation.
- Fixture-based export tests, print CSS, image rasterization for exported copies, portable import.

**Acceptance gate**

- Export a 10+ step fixture in all formats; every output opens, includes correct text/image order, maintains annotations, and contains no broken links.
- No image clips outside PDF margins or overflows Word page width.
- Importing exported `.capture` recovers same steps, screenshots and editable geometry.

### M5 — Harden and package for internal pilot

**Deliverables**

- Friendly error states, reliable exception/cancellation cleanup, diagnostics, preference persistence, asset hygiene, browser/desktop test matrix, signed-build plan.
- Windows distributable folder/ZIP; optional WiX/MSIX packaging following institution's deployment requirements.
- Complete `README.md`, `docs/SECURITY.md`, `docs/MANUAL-TESTS.md`, `docs/STATUS.md`.

**Acceptance gate**

- Fresh Windows machine/VM test with WebView2 runtime prerequisite handled.
- Create/edit/export workflow succeeds without Visual Studio, Node, or SDK installed on end-user machine.
- App never needs Administrator privilege for its supported normal-user scenarios.
- On failures, guide data remains recoverable and no hooks are left installed.

### M6 — Optional hosted publishing, after local release works

Build separate ASP.NET Core API, storage and reader UI; integrate York SSO only when actual approvals/config are available. Implement authenticated permissions and asset authorization before share links. Do not claim publication is ready until the end-to-end security tests pass.

### M7 — Optional Halo KB adapter

Use instance-specific Swagger and approved credentials. Implement safe draft publication, images and metadata mapping, preview, error handling, and article ID roundtrip. No fabricated production integration.

---

## 15. Test strategy — deterministic + manual

### 15.1 Automated unit tests

- State-machine transitions and concurrent start/stop requests.
- Click heuristics and instruction generation for each supported control type.
- UIA fallbacks, null/empty target bounds, stale element, timeout handling.
- Coordinate math for origin `(0,0)`, negative origins, window crops, DPI scaling, image resizing, normalized annotation roundtrip.
- Schema validation, migration, stable UUIDs, and malformed imported guide rejection.
- Project autosave after interrupted writes; SQLite index rebuild from project folders.
- ZIP path traversal, oversized media and malicious content inputs.
- Markdown escaping, export asset paths, DOCX structure, PDF output invariants.
- Redaction flattening in export, with pixel-level tests on synthetic sensitive text blocks.

### 15.2 Editor browser tests

Use Playwright on the React app with a mocked host bridge. Cover editing, reorder, keyboard access, image annotation control, Save state, export request wiring, crash recovery view. Do not require real capture to exercise the editor.

### 15.3 Windows manual test matrix

| Scenario | Minimum result |
|---|---|
| Chrome or Edge browser workflow | Click targets captured; guide usable. |
| Notepad + Settings application | Multiple application contexts captured. |
| Excel or spreadsheet UI (where licensed) | Clicks work; UIA fallback is graceful. |
| Browser → desktop app → browser | One session; ordered across windows. |
| Fast navigation | Pre-action frame or review-marked fallback. |
| Mixed-DPI dual monitors | Highlights correspond to actual clicks. |
| Primary monitor with negative-coordinate secondary | No off-image annotations. |
| Long session with 100 interactions | Responsiveness and bounded memory. |
| Pause and resume | No capture while paused. |
| Cancel | No guide published; temporary capture buffers purged. |
| UAC secure desktop/elevated app | Explicit unsupported/blocked behavior; no bypass. |
| Password input or excluded app | No raw typed value; safe suppression/warning. |
| Crash while editing | Draft recovery successful. |
| Export all four types | Valid documents, correct order, no data loss. |
| High-contrast/keyboard navigation | Accessible minimum control path. |

**Do not record real student/staff personal records in test fixtures.** Use synthetic browser pages and fake Halo-like training fixtures.

### 15.4 Performance/quality targets (aspirational, measure and report)

- Mouse-hook callback returns fast enough that no user-perceptible input lag is introduced; blocking calls forbidden.
- Start capture from hotkey feels immediate (target < 1 second on normal hardware).
- Stop to usable editor for a 20-step recording targeted < 3 seconds after event flush (further image processing can continue without blocking UI).
- Keep per-step capture failures local rather than failing entire session.
- No memory leak across 10 repeated record/edit cycles.
- Ten-minute capture memory bounded and recorded in diagnostics/tests; establish actual baseline after first implementation.
- When targets are not met, report measured value, test setup, and cause; do not make up benchmark numbers.

---

## 16. Development environment and repeatable commands

### Windows development prerequisites

- Windows 11 x64 interactive desktop session.
- .NET 10 SDK with Windows desktop/WPF capabilities.
- Visual Studio 2022 or later (recommended), or compatible .NET CLI + Windows SDK; **actual desktop verification must run on Windows**.
- Node.js supported LTS and npm.
- Git.
- WebView2 Runtime (normally installed on supported Windows images; verify rather than assume).
- Optional `dotnet-ef` tool only if EF Core is selected.

Bootstrap from PowerShell:

```powershell
# Verify tools; install any missing prerequisites before proceeding.
dotnet --info
node --version
npm --version
git --version

# At repository root, Codex should create working scripts:
./scripts/build.ps1
./scripts/test.ps1
./scripts/dev.ps1
```

Required script behaviors:

- `build.ps1`: restore/compile C#; `npm ci`; React build; copy built editor into the WPF distributable directory; fail with nonzero exit code if a step fails.
- `test.ps1`: run C# unit/integration tests, TypeScript/lint, Playwright with mocked host; report manual Windows tests separately.
- `dev.ps1`: launch React dev server + WPF shell in development mode or support a documented static editor build workflow.
- `package.ps1`: create a versioned, self-contained x64 publish folder/ZIP with dependencies and prerequisites documented; do not claim a true single binary if WebView2/assets require companion files.
- `smoke-test.ps1`: verify executable, bundling, import/export fixture, and optionally launch interactive test; fail gracefully on headless CI.

### CI

Use a Windows GitHub Actions runner for build, unit tests and browser editor tests. Headless CI **cannot prove** global hotkey/foreground-window/UIA behavior. Maintain explicit manual interactive test evidence with OS/build, device DPI topology, and results.

### Initial run documentation

`README.md` must include how to start/stop, where projects are stored, how to reopen drafts, how to export, known capture restrictions, how to diagnose shortcut conflicts, privacy warning, and which features are experimental. Screenshots in README can come from generated/synthetic fixtures.

---

## 17. Code quality and project execution rules for Codex

1. **Build vertical slices, not a giant scaffold.** Each milestone ends with a functional demonstration and tests.
2. **No TODOs in required paths.** TODOs are permitted only for explicitly later phases and must have a tracked issue or a clear `STATUS.md` entry.
3. **Do not make up success.** If running on Linux/headless container, compile the cross-platform projects where possible but mark WPF/runtime/UIA validations unverified until Windows manual tests execute.
4. **No fake buttons.** A visible action must perform the corresponding work or be clearly disabled with an accurate explanation.
5. **No placeholder guides as if they were captured.** Use synthetic fixtures only in tests/sample mode, never as the output of a real capture session.
6. **Keep interfaces small and testable.** Capture, UIA, storage, rendering and export should be injected/replaced in tests.
7. **Error handling must retain user work.** Fail one step, not an entire guide; never overwrite files without confirmation.
8. **Sanitize imports and host messages.** No direct command execution from document content.
9. **Document configuration, not secrets.** Check in example environment files but no actual credentials/PII.
10. **Do not deploy, publish, request York OAuth registration, or run against production Halo without explicit authorization.** Build mocks and contract tests first.
11. **Commit frequently**, at least once per green milestone, with meaningful summaries. If Git permissions/remote unavailable, keep local working tree and report precisely.
12. **Track progress honestly** in `docs/STATUS.md` with `Implemented / Tested / Blocked / Not started` and evidence.
13. **Use official documentation** for OS APIs and consult current dependencies when implementing; don't copy outdated P/Invoke examples without validating signatures, lifetime and thread model.
14. **Prefer simplicity over unnecessary abstraction**, except at genuine system boundaries (capture, storage, export, publishing) where interfaces are worth the cost.
15. **No destructive migrations without backup.** Preserve old guide versions or create an explicit migration backup.

### Decision log format

Every substantial deviation gets a short ADR in `docs/DECISIONS.md`:

```markdown
## ADR-004 — Screenshot provider
- Date:
- Choice:
- Alternatives considered:
- Why:
- Known limitations:
- Test evidence:
```

### Status report format (update after every milestone)

```markdown
## Current milestone
M1 — Capture vertical slice

## Implemented
- ...

## Verified
- Build command + outcome
- Unit tests + outcome
- Windows manual steps + OS version + outcome

## Unverified or blocked
- ...

## Next concrete tasks
1. ...
2. ...
3. ...
```

---

## 18. Acceptance scenarios — the finished local MVP

### Scenario 1: Native desktop documentation

**Given** CAPTURE is running in the tray and a Notepad window is open, **when** the user presses the hotkey, makes five ordinary menu/click interactions and stops recording, **then** the editor opens with a persisted guide, ordered instructions, five or fewer meaningfully de-duplicated steps, screenshots showing the relevant interface, and editable highlights.

### Scenario 2: Browser documentation

**Given** a synthetic local training website with a navigation link and form, **when** the user navigates through pages and enters example values, **then** CAPTURE generates steps such as `Select New Article` and `Enter the required information in Title` without storing the entered values or passwords. Screenshots must reflect the pre-action state for navigation clicks when possible.

### Scenario 3: Cross-app workflow

**Given** a browser and a separate desktop application, **when** the user changes focus and continues interacting, **then** a single guide contains chronologically ordered steps from both apps, with application names where available and no dependency on a Chrome extension.

### Scenario 4: Edit and correct

**Given** a captured guide, **when** the user deletes step 2, renames step 3, reorders the remainder and changes a highlight rectangle, **then** Undo/Redo works, autosave persists the changes, and reopening after an application restart shows the updated document.

### Scenario 5: Export

**Given** a reviewed guide, **when** the user exports Markdown, DOCX, PDF and portable JSON package, **then** all four formats are usable, screenshots and annotations are present, and the portable JSON package reimports into an editable guide.

### Scenario 6: Sensitive screenshot

**Given** an image with synthetic secret text, **when** the user destructively redacts the secret and exports the guide, **then** the exported image pixels do not contain the original content. Raw unredacted content is not included in the export archive or reachable through hosted assets.

### Scenario 7: Failure recovery

**Given** the editor or capture worker exits unexpectedly, **when** CAPTURE restarts, **then** a recovered local draft is discoverable, and hooks/sessions can start normally without requiring a machine reboot.

### Definition of done for V1

V1 is done **only** if scenarios 1–7 work in an actual interactive Windows environment, not just with mocked screenshot fixtures; all exports pass checks; no required feature is an inert button; privacy defaults are in place; known limitations are documented; and build/release steps work on a clean Windows machine.

---

## 19. Known implementation hazards — avoid these traps

**Incorrect screenshot moment.** A frame captured after a click shows the new page rather than the target clicked. Implement pre-event imagery early and test fast navigation.

**UIA inside hook callback.** Dangerous: cross-process UIA can block and cause hooks to time out or disappear. Always queue the event and work asynchronously.

**Using only managed legacy UIA blindly.** Check up-to-date UIA3 COM accessibility behavior and compatibility. `FlaUI.UIA3` is a pragmatic wrapper, not a reason to avoid testing WinForms/browser providers. Microsoft UIA overview: https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-uiautomationoverview ; FlaUI repository: https://github.com/FlaUI/FlaUI .

**Assuming UIA element names are truthful.** Custom controls, stale elements and bad accessibility labels are common. Fall back to pointer and review flag.

**Coordinate bugs.** Do not mix WPF device-independent units with physical desktop pixels and screenshot crop pixels. Centralize transformations; test mixed DPI and negative monitor origins.

**Screenshot overlay contamination.** Verify recording toolbar exclusion and do not leave capture controls visibly covering recorded UI. If necessary hide it just for capture and restore without stealing focus.

**“Redaction” as CSS rectangle.** The unredacted image can remain downloadable. Flatten/destructively sanitize exports and publications.

**Rendering content from user-provided JSON into native host methods.** Use allowlisted commands, strict source validation, CSP and sandboxing.

**Hard-coded Halo endpoints.** Configurations differ. Develop against mock adapter and known instance Swagger once available.

**One-file executable claims.** WPF/WebView2/native dependencies may require directory distribution. Deliver a working ZIP/installer instead of an inaccurate “single EXE” claim.

**Scope explosion.** A reliable desktop event capture pipeline is the product's principal risk. Don't build hosted analytics or auto-generated training videos before this works.

---

## 20. Deliverables expected from Codex

At the end of each milestone, Codex must provide:

- Implemented source code in the repository.
- Verified build/test commands and observed results.
- Clear explanation of what **actually works** versus only being scaffolded.
- Short demo instructions a non-developer can follow.
- Updated `docs/STATUS.md` and `docs/DECISIONS.md` where relevant.
- Any unresolved Windows validation steps, without pretending they passed.

At end of V1, deliver:

1. The Windows distributable (ZIP or installer) plus source project.
2. Clear usage guide and screenshot showing real editor output.
3. Reproducible developer build/test scripts and setup.
4. Sample synthetic guide and all export formats.
5. Manual test report, including multi-monitor conditions.
6. Security/privacy limitations, deployment prerequisites, and future integration hooks.

---

## 21. The exact starting prompt for Codex

Copy the text below into Codex once this guide is placed in the repository root:

> Read `CAPTURE_CODEX_BUILD_GUIDE.md` completely. You are implementing CAPTURE, a local-first Windows application that converts desktop interactions into editable step-by-step documentation. Treat the guide as the product contract. First inspect the repository and available development environment, then implement **Milestone M0** end-to-end with a buildable .NET 10 WPF shell, bundled React/WebView2 editor, guide schema, typed bridge, tests and scripts. Create `AGENTS.md` with concise project instructions derived from the guide, plus `docs/STATUS.md` and `docs/DECISIONS.md`. Run the available tests. If the environment is not Windows, do not claim to test the desktop UI; identify the precise Windows validations required. After M0 passes, proceed to **M1 capture vertical slice** without adding server or Halo dependencies. For M1, prioritize a real hotkey → capture → stop → editable guide path over appearance. Implement functionality rather than sample-only mockups, and keep all capture data local. Report what was implemented, tested, and blocked, with the next executable steps. Do not create fake production integrations or silently skip failures.

### Follow-on prompt after M1

> Continue from `docs/STATUS.md`, not from memory. Implement the next incomplete milestone from `CAPTURE_CODEX_BUILD_GUIDE.md`. Preserve existing working paths, add tests, validate build, and update the status/decision logs. Focus on real reliability rather than scaffolding features from future milestones. Stop claiming completion at the first unverified Windows interaction gate and provide a concise manual test plan if interactive Windows access is unavailable.

---

## 22. Further reference materials

Primary API and platform references (verify current signatures and availability during implementation):

- .NET 10 support: https://learn.microsoft.com/dotnet/core/releases-and-support
- WPF on modern .NET: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- RegisterHotKey: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey
- Low-level mouse hook: https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc
- UIA framework: https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-uiautomationoverview
- UIA threading: https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading
- FlaUI UIA3: https://github.com/FlaUI/FlaUI
- Windows Graphics Capture: https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture
- Capture Win32 interop: https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow
- DPI awareness: https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process
- WebView2 WPF: https://learn.microsoft.com/en-us/microsoft-edge/webview2/get-started/wpf
- WebView2 local content: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/working-with-local-content
- WebView2 security: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security
- WebView2 print/PDF: https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/print
- Halo REST API overview (may lag current release): https://haloitsm.com/apidoc/
- Halo API application setup and instance Swagger: https://haloitsm.com/guides/article/?kbid=1823

**Final instruction to Codex:** Make the local capture-to-guide experience excellent and reliable before expanding scope. The immediate test is whether a real user can record a multi-application workflow, stop, and receive an accurate editable guide without uploading anything or manually assembling screenshots. Everything else is secondary.
