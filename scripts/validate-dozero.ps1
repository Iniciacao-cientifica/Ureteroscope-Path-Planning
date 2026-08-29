[CmdletBinding()]
param(
    [switch]$RequireClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$unityProject = Join-Path $repoRoot "Navegacao_Renal_3D\Unity"
$unityExecutable = "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe"
$unityLog = Join-Path $unityProject "Logs\dozero-validation.log"
$transientReport = Join-Path $unityProject "Logs\dozero-validation-report.json"
$firmwareDirectory = Join-Path $repoRoot "hardware\firmware\ureteroscope_controller"
$results = [System.Collections.Generic.List[string]]::new()

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [Parameter(Mandatory)] [string]$Step,
        [string]$WorkingDirectory = $repoRoot
    )

    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$Step falhou com codigo $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Assert-CleanWorktree {
    $status = & git -C $repoRoot status --porcelain
    if ($LASTEXITCODE -ne 0) {
        throw "Nao foi possivel consultar o estado do Git."
    }
    if ($status) {
        throw "O worktree precisa estar limpo para usar -RequireClean.`n$($status -join "`n")"
    }
}

function Test-NewBinaryAssetsUseLfs {
    $added = & git -C $repoRoot diff --cached --name-only --diff-filter=A -- "Navegacao_Renal_3D/**/*.obj" "Navegacao_Renal_3D/**/*.png"
    if ($LASTEXITCODE -ne 0) {
        throw "Nao foi possivel inspecionar novos arquivos binarios."
    }

    foreach ($path in $added) {
        $attribute = & git -C $repoRoot check-attr filter -- $path
        if ($LASTEXITCODE -ne 0 -or $attribute -notmatch ': filter: lfs$') {
            throw "Novo binario ativo fora do Git LFS: $path. Registre o caminho com git lfs track antes do commit."
        }
    }
}

function Resolve-PlatformIo {
    foreach ($commandName in @("platformio", "pio")) {
        $command = Get-Command $commandName -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
    }

    $userProfile = [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)
    foreach ($candidate in @(
        (Join-Path $userProfile ".platformio\penv\Scripts\platformio.exe"),
        (Join-Path $userProfile ".platformio\penv\Scripts\pio.exe")
    )) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "PlatformIO nao encontrado no PATH nem na instalacao padrao do usuario."
}

try {
    Write-Host "[1/6] Verificando Git e arquivos novos..."
    if ($RequireClean) {
        Assert-CleanWorktree
    }
    Invoke-NativeCommand -FilePath "git" -Arguments @("-C", $repoRoot, "diff", "--check") -Step "git diff --check"
    Invoke-NativeCommand -FilePath "git" -Arguments @("-C", $repoRoot, "diff", "--cached", "--check") -Step "git diff --cached --check"
    Test-NewBinaryAssetsUseLfs
    $results.Add("Git: OK")

    Write-Host "[2/6] Verificando objetos Git LFS..."
    Invoke-NativeCommand -FilePath "git" -Arguments @("-C", $repoRoot, "lfs", "fsck") -Step "git lfs fsck"
    $results.Add("Git LFS: OK")

    Write-Host "[3/6] Executando testes Python..."
    Invoke-NativeCommand -FilePath "python" -Arguments @("-m", "unittest", "discover", "-s", "tests", "-v") -Step "Testes Python"
    $results.Add("Python: 4 testes aprovados")

    Write-Host "[4/6] Compilando firmware ESP32..."
    $platformIo = Resolve-PlatformIo
    Invoke-NativeCommand -FilePath $platformIo -Arguments @("run", "-e", "esp32dev") -Step "Build do firmware" -WorkingDirectory $firmwareDirectory
    $results.Add("Firmware esp32dev: OK")

    Write-Host "[5/6] Validando Unity 6000.5.0f1..."
    if (-not (Test-Path -LiteralPath $unityExecutable)) {
        throw "Unity 6000.5.0f1 nao encontrado em: $unityExecutable"
    }
    New-Item -ItemType Directory -Path (Split-Path $unityLog) -Force | Out-Null
    Remove-Item -LiteralPath $transientReport -Force -ErrorAction SilentlyContinue
    $unityArguments = @(
        "-batchmode",
        "-nographics",
        "-quit",
        "-projectPath", "`"$unityProject`"",
        "-executeMethod", "NavegacaoRenal.Editor.Marco6ProjectSetup.Validate",
        "-logFile", "`"$unityLog`""
    )
    $previousReportOverride = [Environment]::GetEnvironmentVariable("NAVEGACAO_RENAL_VALIDATION_REPORT", "Process")
    [Environment]::SetEnvironmentVariable("NAVEGACAO_RENAL_VALIDATION_REPORT", $transientReport, "Process")
    try {
        $unityProcess = Start-Process -FilePath $unityExecutable -ArgumentList $unityArguments -WindowStyle Hidden -Wait -PassThru
    }
    finally {
        [Environment]::SetEnvironmentVariable("NAVEGACAO_RENAL_VALIDATION_REPORT", $previousReportOverride, "Process")
    }
    if ($unityProcess.ExitCode -ne 0) {
        Get-Content $unityLog -Tail 120 -ErrorAction SilentlyContinue
        throw "Validacao Unity falhou com codigo $($unityProcess.ExitCode)."
    }
    if (-not (Test-Path -LiteralPath $transientReport)) {
        throw "Unity terminou sem gerar o relatorio transitorio."
    }
    $unityReport = Get-Content $transientReport -Raw | ConvertFrom-Json
    if (-not $unityReport.passed -or $unityReport.legacyChecks -ne 133 -or
        $unityReport.marco6Checks -ne 61 -or $unityReport.totalChecks -ne 194 -or
        $unityReport.errors.Count -ne 0) {
        throw "Relatorio Unity inesperado: passed=$($unityReport.passed), total=$($unityReport.totalChecks)."
    }
    $results.Add("Unity: 194 verificacoes aprovadas")

    Write-Host "[6/6] Aplicando gate final..."
    if ($RequireClean) {
        Assert-CleanWorktree
    }
    $results.Add("Gate final: OK")

    Write-Host ""
    Write-Host "Validacao DoZero concluida com sucesso:" -ForegroundColor Green
    $results | ForEach-Object { Write-Host "  - $_" }
}
catch {
    Write-Error $_
    exit 1
}
