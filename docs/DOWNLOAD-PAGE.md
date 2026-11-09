# YU Capture download page

The repository-root `index.html` is a standalone, responsive download page. Without JavaScript it links directly to the published v0.5.0-preview.1 Windows installer. When JavaScript is available, it retrieves public GitHub releases (including previews), selects the most recently published release with a Windows x64 package and prefers its Setup EXE over its portable ZIP, and displays earlier downloadable releases. Publishing a new GitHub release updates the page automatically; no HTML edit is required. It shows up to 500 releases and links to the complete GitHub archive.

## Put it online

The live page is **https://oathpros.github.io/Capture/**. `.github/workflows/pages.yml` deploys only `index.html` on main-branch page changes; application source and intermediate files are excluded. GitHub Pages deployment succeeded after PR #1 merged.

Alternatively, upload `index.html` to an existing web server. No build process, server code, or dependencies are required.

Downloads and the dynamic archive require visitors to have access to the GitHub repository and its releases. Private repository releases do not become public by hosting this page; use approved accessible hosting for release files if necessary. The page deliberately embeds no credentials. If the API is unavailable, rate limited or inaccessible, the documented download and GitHub archive links remain available.

## Release naming

Publish the installer as `YU-Capture-<version>-Setup-x64.exe`; the portable fallback is `Capture-<version>-win-x64.zip`. Both names match the packaging script. The page excludes export-example ZIPs, draft releases and releases without a matching Windows package. Package requirements and installation instructions are based on README.md. If these change, update the page as well.

## Verification

Inline JavaScript syntax and simulated release loading were checked, including sorting previews, archive rendering, single-release handling, API failure and rejection of download URLs outside the repository. Live GitHub release metadata, uploaded installer asset and successful GitHub Pages deployment were verified through the connected GitHub tools. Visual browser verification was unavailable in the development environment. The static installer link is pinned to 0.5.0-preview.1; JavaScript refreshes it from later published releases.
