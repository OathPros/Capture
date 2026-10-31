. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    $env:CAPTURE_EXPORT_SAMPLES = Join-Path $RepoRoot 'artifacts/export-samples'
    Invoke-Checked dotnet @('test','tests/Capture.Core.Tests/Capture.Core.Tests.csproj','-c','Release','--no-restore')
    if ($IsWindows -or $PSVersionTable.PSEdition -eq 'Desktop') {
        Invoke-Checked dotnet @('test','tests/Capture.Windows.Tests/Capture.Windows.Tests.csproj','-c','Release','--no-restore')
    } else { Write-Host 'Windows rasterized-image export tests are unrun on this OS; Windows CI runs them.' }
    Push-Location src/capture-editor
    try {
        Invoke-Checked npm @('test')
        if (-not $env:CAPTURE_CHROMIUM_PATH) { Invoke-Checked npx @('playwright','install','chromium') }
        Invoke-Checked npm @('run','test:e2e')
    } finally { Pop-Location }
    Write-Host 'Windows capture, hotkeys, UIA, mixed DPI and WebView2 launch require the manual tests in docs/MANUAL-TESTS.md.'
} finally { Pop-Location }
