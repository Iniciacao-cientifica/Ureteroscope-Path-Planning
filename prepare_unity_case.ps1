param(
    [string]$CaseName = "murillo_sample_case"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "prepare_unity_case.ps1 is retained for compatibility and now generates a schema-v2 case."
Push-Location $RepoRoot
try {
    python -B vr_case_pipeline.py --case-id $CaseName --output-dir cases
    if ($LASTEXITCODE -ne 0) {
        throw "Case generation failed."
    }
    python -B sync_unity_case.py (Join-Path "cases" $CaseName) --unity-project UnityVRPrototype
    if ($LASTEXITCODE -ne 0) {
        throw "Unity synchronization failed."
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Open UnityVRPrototype and use Murillo VR > Setup Sample Scene."
