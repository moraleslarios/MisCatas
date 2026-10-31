@echo off
REM Doble clic aqui para arrancar MisCatas y abrir la direccion de la RED LOCAL
REM en HTTPS (https://192.168.x.x:5443), que es la que usan los moviles y la tele.
REM
REM Para que entren desde otros aparatos hay que haber abierto los puertos una
REM vez con:  tools\abrir-puerto-firewall.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Arrancar.ps1" -Lan %*
