@echo off
REM Doble clic aqui para arrancar MisCatas.
REM
REM Abre el navegador en http://localhost:5081, sin cifrar y a proposito: es la
REM direccion que funciona en todos los aparatos de casa. El certificado de
REM HTTPS lo firmamos nosotros y el iPhone no deja ni aceptar el aviso.
REM
REM Tambien queda disponible https://localhost:5443 para quien quiera cifrado.
REM
REM Variantes (por si se lanza desde consola):
REM   Arrancar.cmd -Https   -> abre por HTTPS en lugar de HTTP
REM   Arrancar.cmd -Lan     -> abre la direccion de la red local (moviles/tele)
REM   Arrancar.cmd -Compilar
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Arrancar.ps1" %*
