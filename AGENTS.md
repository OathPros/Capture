# Capture development
Read CAPTURE_CODEX_BUILD_GUIDE.md and docs/STATUS.md. Use the existing checkout; this cloud task is isolated and needs no additional worktree unless requested.
Build a real local-first Windows capture application. Follow milestones in order. Never claim interactive Windows tests passed from Linux or headless CI.
Use .NET 10 (global.json), Node 24 LTS and npm ci. On Windows run scripts/build.ps1, scripts/test.ps1, scripts/package.ps1. Native capture requires Windows 11 x64 and WebView2 Runtime.
Keep hook callbacks limited to copying events into the bounded queue. UIA runs on an MTA worker. Never read arbitrary keyboard input or editable values. No upload, telemetry, hosted service or Halo credential is required.
Treat JSON and bridge requests as untrusted. Validate origin, command allowlist, paths, schema, annotations and archive limits. Never add unrestricted filesystem or shell commands to the bridge.
Keep docs/STATUS.md honest, including failures and Windows manual gates. Preserve user work on save errors; do not overwrite exports without confirmation. Commit meaningful verified increments.
