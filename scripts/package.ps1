. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if (Test-Path artifacts/Capture-win-x64) { Remove-Item artifacts/Capture-win-x64 -Recurse -Force }
    Invoke-Checked dotnet @('publish','src/Capture.App/Capture.App.csproj','-c','Release','--no-restore','-o','artifacts/Capture-win-x64')
    foreach ($file in @('Capture.App.exe','editor/index.html','WebView2Loader.dll')) {
        if (-not (Test-Path "artifacts/Capture-win-x64/$file")) { throw "Missing packaged file: $file" }
    }
    Copy-Item README.md,docs/RELEASE-NOTES.md artifacts/Capture-win-x64
    Compress-Archive -Path artifacts/Capture-win-x64/* -DestinationPath artifacts/Capture-0.1.0-preview.1-win-x64.zip -Force
    Get-FileHash artifacts/Capture-0.1.0-preview.1-win-x64.zip -Algorithm SHA256 | Format-List
} finally { Pop-Location }
