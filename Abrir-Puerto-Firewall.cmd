@echo off
REM ============================================================
REM  Abre en el firewall de Windows los puertos de MisCatas, para
REM  que los moviles y la tele de casa puedan entrar en la web.
REM
REM  Doble clic aqui. Windows pedira permiso (ventana azul de UAC):
REM  hay que decir SI, porque tocar el firewall necesita permisos de
REM  administrador.
REM
REM  Solo hace falta ejecutarlo UNA VEZ por ordenador. Repetirlo no
REM  rompe nada: si las reglas ya estan, las deja como estan.
REM ============================================================

REM "net session" solo funciona siendo administrador: asi sabemos si ya
REM venimos elevados o si hay que volver a lanzarnos pidiendo permiso.
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo  Hacen falta permisos de administrador: pidiendolos...
    echo  Acepta la ventana de Windows que va a aparecer.
    echo.
    REM Relanzamos el script de PowerShell ya elevado. Los argumentos van como
    REM lista entre comillas simples para que no se rompa nada si alguna
    REM carpeta de la ruta llevara espacios. El -NoExit deja la ventana
    REM abierta al acabar, que si no se cierra de golpe y no se lee nada.
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','%~dp0tools\abrir-puerto-firewall.ps1'"
    exit /b
)

REM Si llegamos aqui ya somos administradores (alguien uso el boton derecho).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\abrir-puerto-firewall.ps1" %*

echo.
pause
