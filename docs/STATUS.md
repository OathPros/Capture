# Current milestone
Version 4 quality-of-life update: meaningful capture text, fitted highlights, real strip cuts, and direct clipboard exports.

## Implemented
- UIA target resolver walks at most five elements to find a meaningful interactive control. A dedicated MTA hover sampler caches pre-click labels/bounds; event handling can fall back to its own lookup. Password suppression remains. Edit.Name and editable values are never read; separately exposed static labels and conservative whitelisted identifiers can name fields.
- Generated titles use sentence-case action prose and literal bold field/control labels. Detail instructions default blank. Exact redundant legacy details are cleared and safe old generated labels inferred; distinct user instructions are preserved. Manual title text selection can add/remove bold fields.
- Highlights fit valid physical-pixel UIA rectangles, with bounded padding, frame containment and oversized/off-target rejection; fallback is a review-marked click ring. Existing captured rectangles are not retrospectively inferred from images.
- Non-destructive horizontal/vertical Cut strip removes sections and joins remaining pixels. Original-image cut intervals, merged axis segments and source/display projection keep crop/zoom/annotation coordinates consistent. Vector overlays render after image composition; privacy pixel operations render before it. Undo/reset and every export use the same layout.
- Copy HTML code renders the complete self-contained HTML document in memory. Copy steps for Word generates standard RTF with Unicode, bold field runs and embedded PNGs, plus plain-text fallback; it intentionally omits CF_HTML to avoid Word ignoring data-URL images. Native clipboard setting occurs on the UI STA after rendering succeeds. No temporary export files.
- Schema 1.2 reads 1.0/1.1 and preserves original metadata before first upgrade save. Prior screenshot tools, export privacy guards and packaged WebView2 image-preview smoke check remain.

## Validation in progress
- Core, editor and Windows-targeted compilation checks are run with the pinned toolchain and lockfiles.
- Tests cover title/field cases, legacy details, negative-origin bounds, cut interval projection/validation, HTML/RTF/document formatting, actual cut pixels in DOCX/MD/HTML, native clipboard formats and a real Windows button/text field provider. The synthetic editable field has a deliberately sensitive-looking accessible name/value that must not be read into the title.
- Browser checks exercise cut/apply/undo/reset, manual field formatting, copy-before-save with no export dialog, clipboard error recovery and existing screenshot tools.
- The exact Word clipboard RTF opened and rendered in LibreOffice with its screenshot and bold field labels. Microsoft Word's paste preference can override rich formatting; use Keep Source Formatting.

## Remaining limits
Full real-world capture/UIA timing, mixed DPI, private-window detection and app-specific provider quirks remain manual validation. Missing accessibility metadata produces a review-marked fallback. Crop/cuts retain originals in editable projects; use permanent solid redaction for secrets. PDF, manual image insertion, signing, WGC replacement and hosted integrations remain unfinished.
