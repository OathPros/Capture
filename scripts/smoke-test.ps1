. "$PSScriptRoot/common.ps1"
$path = Join-Path $RepoRoot 'artifacts/Capture-win-x64'
foreach ($file in @('Capture.App.exe','Capture.App.dll','editor/index.html','WebView2Loader.dll')) {
    if (-not (Test-Path "$path/$file")) { throw "Missing packaged file: $file" }
}
$assets = Get-ChildItem "$path/editor/assets/*.js"
if ($assets.Count -eq 0) { throw 'The bundled editor is missing.' }
Write-Host 'Package structure passed. This does not launch or verify desktop capture. Follow docs/MANUAL-TESTS.md on interactive Windows 11.'
