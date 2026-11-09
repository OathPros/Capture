. "$PSScriptRoot/common.ps1"
# Windows CI: exercise real silent install/upgrade/uninstall in an isolated directory.
$installer = Get-ChildItem "$RepoRoot/artifacts/YU-Capture-*-Setup-x64.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $installer) { throw 'Missing Windows installer.' }
$target = Join-Path ([IO.Path]::GetTempPath()) ('yu-capture-install-' + [Guid]::NewGuid().ToString('N'))
$projectSentinel = Join-Path $target 'user-project-sentinel.json'
try {
    foreach ($pass in 1..2) {
        $process = Start-Process $installer.FullName -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=`"$target`"") -Wait -PassThru
        if ($process.ExitCode -ne 0) { throw "Installer failed with exit code $($process.ExitCode)" }
        foreach ($file in @('Capture.App.exe','Capture.App.dll','editor/index.html','editor/capture-icon.png','editor/capture.ico','WebView2Loader.dll','unins000.exe')) {
            if (-not (Test-Path (Join-Path $target $file))) { throw "Installer is missing $file" }
        }
        $expected = (Get-FileHash "$RepoRoot/artifacts/Capture-win-x64/Capture.App.dll").Hash
        if ((Get-FileHash "$target/Capture.App.dll").Hash -ne $expected) { throw 'Installed application differs from packaged build.' }
        if ($pass -eq 1) { Set-Content $projectSentinel '{"preserve":true}' }
        if (-not (Test-Path $projectSentinel)) { throw 'Upgrade deleted user-created files.' }
    }
    # Launch the installed app and verify its real WebView2 screenshot bridge.
    $env:CAPTURE_SMOKE_RESULT = Join-Path $target 'smoke-result.txt'
    $app = Start-Process "$target/Capture.App.exe" -ArgumentList '--smoke-test' -PassThru
    if (-not $app.WaitForExit(90000)) { $app.Kill(); throw 'Installed app smoke test timed out.' }
    if ($app.ExitCode -ne 0 -or -not (Test-Path $env:CAPTURE_SMOKE_RESULT)) { throw 'Installed app did not start correctly.' }
    if (-not (Get-Content $env:CAPTURE_SMOKE_RESULT -Raw).StartsWith('PASS:')) { throw 'Installed WebView2 screenshot test failed.' }
    $uninstall = Start-Process "$target/unins000.exe" -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
    if ($uninstall.ExitCode -ne 0) { throw 'Uninstall failed.' }
    if (Test-Path "$target/Capture.App.exe") { throw 'Uninstall left the app executable behind.' }
    if (-not (Test-Path $projectSentinel)) { throw 'Uninstall deleted user-created files.' }
    Write-Host 'Installer, repeat installation, installed-app smoke test and uninstall passed.'
} finally {
    Remove-Item Env:CAPTURE_SMOKE_RESULT -ErrorAction SilentlyContinue
    # This test directory never contains real user projects.
    Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
}
