. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    Invoke-Checked dotnet @('test','tests/Capture.Core.Tests/Capture.Core.Tests.csproj','-c','Release','--no-restore')
    Push-Location src/capture-editor
    try {
        Invoke-Checked npm @('test')
        if (-not $env:CAPTURE_CHROMIUM_PATH) { Invoke-Checked npx @('playwright','install','chromium') }
        Invoke-Checked npm @('run','test:e2e')
    } finally { Pop-Location }
    Write-Host 'Windows capture, hotkeys, UIA, mixed DPI and WebView2 launch require the manual tests in docs/MANUAL-TESTS.md.'
} finally { Pop-Location }
