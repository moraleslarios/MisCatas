@echo off
REM Doble clic aqui para parar MisCatas.
REM Cierra el servidor y libera los dos puertos: 5443 (HTTPS) y 5080 (HTTP).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Parar.ps1" %*
timeout /t 3 >nul
