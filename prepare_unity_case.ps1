param(
    [string]$CaseName = "murillo_sample_case"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ExportDir = Join-Path $RepoRoot "exports\$CaseName"
$UnityAssets = Join-Path $RepoRoot "UnityVRPrototype\Assets"
$StreamingAssets = Join-Path $UnityAssets "StreamingAssets"
$Models = Join-Path $UnityAssets "Models"

Write-Host "Generating VR export for case: $CaseName"
Push-Location $RepoRoot
try {
    python -B vr_export_pipeline.py --case-name $CaseName
}
finally {
    Pop-Location
}

New-Item -ItemType Directory -Force -Path $StreamingAssets | Out-Null
New-Item -ItemType Directory -Force -Path $Models | Out-Null

$RouteSource = Join-Path $ExportDir "vr_route_unity.json"
$MeshSource = Join-Path $ExportDir "urinary_tract_unity.obj"
$RouteTarget = Join-Path $StreamingAssets "vr_route_unity.json"
$MeshTarget = Join-Path $Models "urinary_tract_unity.obj"

Copy-Item -LiteralPath $RouteSource -Destination $RouteTarget -Force
Copy-Item -LiteralPath $MeshSource -Destination $MeshTarget -Force

Write-Host "Copied route JSON to: $RouteTarget"
Write-Host "Copied anatomy OBJ to: $MeshTarget"

Push-Location $RepoRoot
try {
    python -B -c "import json; from pathlib import Path; d=json.loads(Path('UnityVRPrototype/Assets/StreamingAssets/vr_route_unity.json').read_text(encoding='utf-8')); assert d['metrics']['outside_points']==0; assert len(d['path_smoothed'])>0; print('Unity case ready:', d['case_name'], 'points:', len(d['path_smoothed']), 'outside:', d['metrics']['outside_points'], 'risk:', d['metrics']['risk_points'])"
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Open UnityVRPrototype in Unity. The Editor setup script will create the VR Case Loader automatically."
Write-Host "If it does not appear, use the Unity menu: Murillo VR > Setup Sample Scene."
