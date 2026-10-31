@echo off
REM Doble clic aqui para arrancar MisCatas.
REM
REM Abre el navegador en la direccion SEGURA: https://localhost:5443
REM Tambien queda disponible http://localhost:5080, que redirige al seguro.
REM
REM Variantes (por si se lanza desde consola):
REM   Arrancar.cmd -Http    -> abre por HTTP en lugar de HTTPS
REM   Arrancar.cmd -Lan     -> abre la direccion de la red local (moviles/tele)
REM   Arrancar.cmd -Compilar
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Arrancar.ps1" %*
