. "$PSScriptRoot/common.ps1"
if (-not $IsWindows -and $PSVersionTable.PSEdition -ne 'Desktop') { throw 'The native application requires Windows 11.' }
& "$PSScriptRoot/build.ps1"
Push-Location $RepoRoot
try { Invoke-Checked dotnet @('run','--project','src/Capture.App','-c','Release','--no-build') } finally { Pop-Location }
