@echo off
REM Doble clic aqui para arrancar MisCatas.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Arrancar.ps1" %*
