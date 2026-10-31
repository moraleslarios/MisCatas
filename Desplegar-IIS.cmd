@echo off
REM ============================================================
REM  Instala MisCatas en el IIS de este equipo.
REM
REM  Al terminar la web estara en:  https://miscatas.es
REM  y arrancara sola al encender el ordenador.
REM
REM  IMPORTANTE: hay que ejecutarlo como ADMINISTRADOR.
REM  Boton derecho sobre este fichero -> "Ejecutar como administrador".
REM
REM  Se puede repetir cuantas veces se quiera: tambien sirve para
REM  actualizar la web despues de hacer cambios.
REM ============================================================

REM Comprobacion de permisos: "net session" solo funciona siendo administrador.
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo  ============================================================
    echo   HACEN FALTA PERMISOS DE ADMINISTRADOR
    echo  ============================================================
    echo.
    echo   Cierra esta ventana y vuelve a abrir este fichero con el
    echo   boton DERECHO del raton, eligiendo:
    echo.
    echo       "Ejecutar como administrador"
    echo.
    echo  ============================================================
    echo.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\desplegar-iis.ps1" %*

echo.
pause
