# YU Capture download page

The repository-root `index.html` is a standalone, responsive download page. Without JavaScript it links to GitHub Releases, with the documented v0.4.0-preview.1 portable ZIP available as a fallback. When JavaScript is available, it retrieves public GitHub releases (including previews), selects the most recently published release with a Windows x64 package and prefers its Setup EXE over its portable ZIP, and displays earlier downloadable releases. Publishing a new GitHub release updates the page automatically; no HTML edit is required. It shows up to 500 releases and links to the complete GitHub archive.

## Put it online

For GitHub Pages, open this repository's **Settings → Pages**, choose **Deploy from a branch**, select the branch containing this page and **/ (root)**, and save. GitHub will show the actual shareable URL after deployment. The expected project URL is `https://oathpros.github.io/Capture/` when Pages is enabled with that default domain. Hosting has not been enabled by this change.

Alternatively, upload `index.html` to an existing web server. No build process, server code, or dependencies are required.

Downloads and the dynamic archive require visitors to have access to the GitHub repository and its releases. Private repository releases do not become public by hosting this page; use approved accessible hosting for release files if necessary. The page deliberately embeds no credentials. If the API is unavailable, rate limited or inaccessible, the documented download and GitHub archive links remain available.

## Release naming

Publish the installer as `YU-Capture-<version>-Setup-x64.exe`; the portable fallback is `Capture-<version>-win-x64.zip`. Both names match the packaging script. The page excludes export-example ZIPs, draft releases and releases without a matching Windows package. Package requirements and installation instructions are based on README.md. If these change, update the page as well.

## Verification

Inline JavaScript syntax and simulated release loading were checked, including sorting previews, archive rendering, single-release handling, API failure and rejection of download URLs outside the repository. Live GitHub and browser verification were unavailable in the development environment. The fallback version is based on docs/STATUS.md, not a fresh network check.
