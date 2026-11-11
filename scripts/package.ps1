. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    if (Test-Path artifacts/Capture-win-x64) { Remove-Item artifacts/Capture-win-x64 -Recurse -Force }
    Invoke-Checked dotnet @('publish','src/Capture.App/Capture.App.csproj','-c','Release','--no-restore','-o','artifacts/Capture-win-x64')
    foreach ($file in @('Capture.App.exe','editor/index.html','editor/capture-icon.png','editor/capture.ico','WebView2Loader.dll')) {
        if (-not (Test-Path "artifacts/Capture-win-x64/$file")) { throw "Missing packaged file: $file" }
    }
    [xml]$project = Get-Content src/Capture.App/Capture.App.csproj
    $version = [string]$project.Project.PropertyGroup.Version
    if ($version -notmatch '^(\d+)\.(\d+)(?:\.(\d+))?(?:-[0-9A-Za-z.-]+)?$') { throw 'Unsupported package version.' }
    $patchVersion = if ($Matches[3]) { $Matches[3] } else { '0' }
    $numericVersion = "$($Matches[1]).$($Matches[2]).$patchVersion.0"
    Copy-Item README.md,docs/RELEASE-NOTES.md artifacts/Capture-win-x64
    $portableZip = "artifacts/Capture-$version-win-x64.zip"
    Compress-Archive -Path artifacts/Capture-win-x64/* -DestinationPath $portableZip -Force
    if (Test-Path artifacts/export-samples/synthetic-guide.html) {
        Compress-Archive -Path artifacts/export-samples/* -DestinationPath artifacts/Capture-export-examples.zip -Force
    }
    # Bundle Microsoft's signed bootstrapper; Setup downloads the runtime only if missing.
    $dependencyDirectory = Join-Path $RepoRoot 'artifacts/installer-dependencies'
    New-Item -ItemType Directory -Force $dependencyDirectory | Out-Null
    $bootstrapper = Join-Path $dependencyDirectory 'MicrosoftEdgeWebview2Setup.exe'
    Invoke-WebRequest 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrapper
    $signature = Get-AuthenticodeSignature $bootstrapper
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'WebView2 bootstrapper does not have a valid Microsoft signature.'
    }
    $compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
    if (-not (Test-Path $compiler)) { throw 'Install Inno Setup 6 to build the Windows installer.' }
    Invoke-Checked $compiler @("/DAppVersion=$version", "/DNumericVersion=$numericVersion", 'installer/Capture.iss')
    Get-FileHash "artifacts/YU-Capture-$version-Setup-x64.exe" -Algorithm SHA256 | Format-List
    Get-FileHash $portableZip -Algorithm SHA256 | Format-List
} finally { Pop-Location }
