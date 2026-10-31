@echo off
REM ============================================================
REM  Quita MisCatas del IIS de este equipo y lo deja como estaba.
REM
REM  NO borra los datos de las catas: siguen en la base de datos.
REM  NO desinstala IIS (puede estar usandolo otro programa).
REM
REM  IMPORTANTE: hay que ejecutarlo como ADMINISTRADOR.
REM  Boton derecho sobre este fichero -> "Ejecutar como administrador".
REM
REM  Variante (desde consola):
REM    Retirar-IIS.cmd -BorrarCarpeta   -> borra tambien los ficheros
REM ============================================================

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

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\retirar-iis.ps1" %*

echo.
pause
