# Preview security and privacy

Capture is explicitly started, visible and local-only. It never installs a keyboard hook or reads UIA ValuePattern. Screenshot buffering stops on pause/finish; selected screenshots are stored under the user's LocalAppData. A password control identified by UIA suppresses that click, but surrounding screen content may still contain secrets. Process exclusions are case-insensitive substrings of foreground/clicked process names. Private-window detection is not implemented. Use synthetic data; no redaction tool is included.

The low-level mouse callback only copies position/monotonic time into a bounded channel and calls the next hook. No disk/UIA/screenshots/network run in it. A separate MTA worker handles accessibility; screenshots use a separate sampler. A hung provider requires process restart; saved drafts are retained.

WebView2 loads only the bundled capture.local page with a strict CSP. Navigation, downloads, new windows and permissions are rejected. Each host message checks origin/version/allowlist and data size. Project asset identity cannot be changed through editor saves. File dialogs select import/export paths; there is no arbitrary file or shell bridge. Project data is treated as plain text.

Imports validate relative paths, duplicate entries, total/archive entry sizes, count, schema, annotation bounds, image checksums and PNG headers/dimensions. Extract only referenced assets into a new GUID project folder; roll back failed imports. Projects, exports and WebView2 data are not encrypted by this app. Treat portable packages as containing original screenshots. Existing external exports/backups are not removed by deleting or editing a project.

No release signing has been configured. No server/SSO/Halo/telemetry integration is included. Follow enterprise application approval before deployment. Do not use this preview for institutional personal data until interactive tests and privacy/security review are complete.

Document exports validate source image dimensions/checksums before rasterizing highlight copies. DOCX is generated as Open XML without macros or external image relationships. Markdown escapes user text and bundles relative image assets. HTML escapes all user fields, embeds PNG bytes as data URLs, permits only inline styling/data images through CSP, and contains no script or external resources. Export to a temporary sibling file and replace the destination only after success. These formats retain visible screenshot content; highlight outlines are not redaction.
