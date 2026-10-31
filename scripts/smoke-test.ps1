. "$PSScriptRoot/common.ps1"
$path = Join-Path $RepoRoot 'artifacts/Capture-win-x64'
foreach ($file in @('Capture.App.exe','Capture.App.dll','editor/index.html','WebView2Loader.dll')) {
    if (-not (Test-Path "$path/$file")) { throw "Missing packaged file: $file" }
}
$assets = Get-ChildItem "$path/editor/assets/*.js"
if ($assets.Count -eq 0) { throw 'The bundled editor is missing.' }
if ($IsWindows -or $PSVersionTable.PSEdition -eq 'Desktop') {
    $result = Join-Path ([System.IO.Path]::GetTempPath()) ("capture-smoke-" + [Guid]::NewGuid().ToString() + ".txt")
    $env:CAPTURE_SMOKE_RESULT = $result
    try {
        $process = Start-Process -FilePath "$path/Capture.App.exe" -ArgumentList '--smoke-test' -PassThru
        if (-not $process.WaitForExit(90000)) { $process.Kill(); throw 'Native screenshot smoke test timed out before the image decoded.' }
        if (-not (Test-Path $result)) { throw 'Native screenshot smoke test failed to start WebView2. Check that WebView2 Runtime is installed.' }
        $message = Get-Content $result -Raw
        if ($process.ExitCode -ne 0 -or -not $message.StartsWith('PASS:')) { throw $message }
        Write-Host $message
    } finally { Remove-Item Env:CAPTURE_SMOKE_RESULT -ErrorAction SilentlyContinue; Remove-Item $result -ErrorAction SilentlyContinue }
} else { Write-Host 'Package structure passed. Native WebView2 screenshot decoding is unrun on this OS; Windows CI runs it.' }
Write-Host 'Interactive capture, hotkeys and UIA still require docs/MANUAL-TESTS.md.'
