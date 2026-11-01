@echo off
REM Doble clic aqui para arrancar MisCatas y abrir la direccion de la RED LOCAL
REM (http://192.168.x.x:5081), que es la que usan los moviles y la tele.
REM
REM Va sin cifrar a proposito: el certificado de HTTPS lo firmamos nosotros y
REM el iPhone no ofrece la opcion de confiar, asi que desde un iPhone la web
REM era inalcanzable. En una red de casa, y para unas notas de cata, poder
REM entrar importa mas que cifrar.
REM
REM Para que entren desde otros aparatos hay que haber abierto los puertos una
REM vez con:  tools\abrir-puerto-firewall.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Arrancar.ps1" -Lan %*
