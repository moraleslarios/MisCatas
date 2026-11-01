@echo off
REM Doble clic aqui para parar MisCatas.
REM Cierra el servidor y libera los dos puertos: 5081 (HTTP) y 5443 (HTTPS).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Parar.ps1" %*
timeout /t 3 >nul
