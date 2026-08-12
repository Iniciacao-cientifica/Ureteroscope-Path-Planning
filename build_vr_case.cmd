@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build_vr_case.ps1" %*
