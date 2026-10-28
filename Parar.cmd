@echo off
REM Doble clic aqui para parar MisCatas.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Parar.ps1" %*
timeout /t 3 >nul
