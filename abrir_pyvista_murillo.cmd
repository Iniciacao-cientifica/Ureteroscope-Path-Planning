@echo off
cd /d "%~dp0"
echo Abrindo PyVista pelo PowerShell...
powershell -NoProfile -ExecutionPolicy Bypass -NoExit -File "%~dp0abrir_pyvista_vscode.ps1"
pause
