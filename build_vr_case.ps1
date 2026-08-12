param(
    [string]$MaskDir = "map",
    [string]$StoneMask = "",
    [string]$CaseId = "murillo_sample_case",
    [string]$DisplayName = "",
    [string]$Start = "253,355,20",
    [string]$Target = "220,174,10",
    [string]$SpacingMm = "",
    [string]$UnityProject = "UnityVRPrototype",
    [string]$UnityEditor = "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe",
    [switch]$BuildApk,
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$UnityProjectPath = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $UnityProject))
$CasesRoot = Join-Path $RepoRoot "cases"
$CaseOutput = Join-Path $CasesRoot $CaseId
$BuildDirectory = Join-Path $UnityProjectPath "Builds"
$ApkPath = Join-Path $BuildDirectory "UreteroscopyVR.apk"
$StagedProject = $null

if ($Install) {
    $BuildApk = $true
}

$pipelineArguments = @(
    "vr_case_pipeline.py",
    "--mask", $MaskDir,
    "--case-id", $CaseId,
    "--output-dir", $CasesRoot,
    "--start", $Start,
    "--target", $Target
)
if ($StoneMask) {
    $pipelineArguments += @("--stone-mask", $StoneMask)
}
if ($DisplayName) {
    $pipelineArguments += @("--display-name", $DisplayName)
}
if ($SpacingMm) {
    $pipelineArguments += @("--spacing-mm", $SpacingMm)
}

Push-Location $RepoRoot
try {
    Write-Host "[1/4] Generating and validating case $CaseId"
    & python @pipelineArguments
    if ($LASTEXITCODE -ne 0) {
        throw "The Python case pipeline failed with exit code $LASTEXITCODE."
    }

    Write-Host "[2/4] Synchronizing case with Unity"
    & python sync_unity_case.py $CaseOutput --unity-project $UnityProjectPath
    if ($LASTEXITCODE -ne 0) {
        throw "Unity case synchronization failed with exit code $LASTEXITCODE."
    }

    if (-not $BuildApk) {
        Write-Host "Case is ready for an Editor test. Use -BuildApk after Android Build Support is installed."
        exit 0
    }

    if (-not (Test-Path -LiteralPath $UnityEditor)) {
        throw "Unity Editor was not found at $UnityEditor. Pass -UnityEditor with the correct path."
    }
    $EditorRoot = Split-Path -Parent $UnityEditor
    $AndroidPlayer = Join-Path $EditorRoot "Data\PlaybackEngines\AndroidPlayer"
    if (-not (Test-Path -LiteralPath $AndroidPlayer)) {
        throw "Android Build Support is missing. In Unity Hub, add Android Build Support, SDK & NDK Tools, and OpenJDK to Unity 6000.5.0f1."
    }

    New-Item -ItemType Directory -Path $BuildDirectory -Force | Out-Null
    $UnityBuildProjectPath = $UnityProjectPath
    $UnityBuildApkPath = $ApkPath
    if ($UnityProjectPath -match '[^\x00-\x7F]') {
        $StagedProject = Join-Path ([System.IO.Path]::GetTempPath()) ("UreteroscopyVRBuild_" + $PID + "_" + (Get-Date -Format "yyyyMMddHHmmss"))
        New-Item -ItemType Directory -Path $StagedProject -Force | Out-Null
        Write-Host "Staging Unity project at ASCII-only path: $StagedProject"
        Copy-Item -LiteralPath (Join-Path $UnityProjectPath "Assets") -Destination $StagedProject -Recurse
        Copy-Item -LiteralPath (Join-Path $UnityProjectPath "Packages") -Destination $StagedProject -Recurse
        Copy-Item -LiteralPath (Join-Path $UnityProjectPath "ProjectSettings") -Destination $StagedProject -Recurse
        $UnityBuildProjectPath = $StagedProject
        $UnityBuildApkPath = Join-Path $StagedProject "Builds\UreteroscopyVR.apk"
    }

    Write-Host "[3/4] Building Quest APK"
    & $UnityEditor -batchmode -quit -projectPath $UnityBuildProjectPath -executeMethod QuestBuild.BuildApk -murilloOutput $UnityBuildApkPath -logFile -
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $UnityBuildApkPath)) {
        throw "Unity Android build failed. Review the Unity batch log above."
    }
    if ($UnityBuildApkPath -ne $ApkPath) {
        Copy-Item -LiteralPath $UnityBuildApkPath -Destination $ApkPath -Force
    }

    if (-not $Install) {
        Write-Host "APK created at $ApkPath"
        exit 0
    }

    $Adb = Join-Path $AndroidPlayer "SDK\platform-tools\adb.exe"
    if (-not (Test-Path -LiteralPath $Adb)) {
        throw "ADB was not found in Unity Android Build Support."
    }
    $Devices = & $Adb devices
    if ($Devices -notmatch "\tdevice") {
        throw "No authorized Quest was found. Enable Developer Mode and accept USB debugging inside the headset."
    }

    Write-Host "[4/4] Installing APK on Quest"
    & $Adb install -r $ApkPath
    if ($LASTEXITCODE -ne 0) {
        throw "ADB installation failed."
    }
    Write-Host "Build installed. Put on the Quest and open Murillo Ureteroscopy VR."
}
finally {
    Pop-Location
    if ($StagedProject -and (Test-Path -LiteralPath $StagedProject)) {
        $ResolvedStage = [System.IO.Path]::GetFullPath($StagedProject)
        $ResolvedTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if ($ResolvedStage.StartsWith($ResolvedTemp, [System.StringComparison]::OrdinalIgnoreCase)) {
            Remove-Item -LiteralPath $ResolvedStage -Recurse -Force
        }
    }
}
