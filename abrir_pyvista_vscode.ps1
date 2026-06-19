$ErrorActionPreference = "Stop"

Set-Location -LiteralPath $PSScriptRoot

$env:PYVISTA_OFF_SCREEN = "false"
$env:VTK_DEFAULT_RENDER_WINDOW_OFFSCREEN = "0"

Write-Host ""
Write-Host "Abrindo PyVista em janela interativa..." -ForegroundColor Cyan
Write-Host "Projeto: $PSScriptRoot"
Write-Host ""
Write-Host "Se faltar alguma dependencia, rode:"
Write-Host "python -m pip install -r requirements-pyvista.txt" -ForegroundColor Yellow
Write-Host ""

python -B pyvista_demo.py

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "O PyVista terminou com erro. Veja as mensagens acima." -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "PyVista fechado." -ForegroundColor Green
