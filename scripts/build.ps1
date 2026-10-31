. "$PSScriptRoot/common.ps1"
Push-Location $RepoRoot
try {
    Push-Location src/capture-editor
    try { Invoke-Checked npm @('ci'); Invoke-Checked npm @('run','build') } finally { Pop-Location }
    Invoke-Checked dotnet @('restore','Capture.slnx','--locked-mode')
    Invoke-Checked dotnet @('build','Capture.slnx','-c','Release','--no-restore')
} finally { Pop-Location }
