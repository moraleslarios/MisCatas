#requires -Version 5.1
<#
    Crea los accesos directos en el Escritorio para arrancar y parar MisCatas
    sin tocar la linea de comandos.

    Uso:
        .\tools\crear-accesos-directos.ps1

    Se pueden volver a ejecutar sin problema: sobreescribe los existentes.

    Los tres iconos que deja:
        MisCatas - ARRANCAR        -> abre https://localhost:5443 (seguro)
        MisCatas - ARRANCAR (RED)  -> abre https://192.168.x.x:5443 (moviles/tele)
        MisCatas - PARAR           -> cierra la web
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# El script vive en \tools, asi que la raiz del proyecto es la carpeta de arriba.
$raiz = Split-Path -Parent $PSScriptRoot
$escritorio = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell

# Iconos del propio Windows: imageres.dll tiene una flecha de "play" y un circulo rojo.
$iconos = Join-Path $env:SystemRoot 'System32\imageres.dll'

$atajos = @(
    @{ Nombre = 'MisCatas - ARRANCAR';       Destino = 'Arrancar.cmd';     Icono = "$iconos,100"; Descripcion = 'Levanta la web de MisCatas en https://localhost:5443 y abre el navegador' }
    @{ Nombre = 'MisCatas - ARRANCAR (RED)'; Destino = 'Arrancar-Red.cmd'; Icono = "$iconos,165"; Descripcion = 'Levanta la web y abre la direccion HTTPS de la red local (moviles y tele)' }
    @{ Nombre = 'MisCatas - PARAR';          Destino = 'Parar.cmd';        Icono = "$iconos,98";  Descripcion = 'Cierra la web de MisCatas y libera los puertos 5443 y 5081' }
)

Write-Host ''
Write-Host '=== Creando accesos directos en el Escritorio ===' -ForegroundColor Cyan
Write-Host ''

foreach ($atajo in $atajos) {
    $destino = Join-Path $raiz $atajo.Destino

    if (-not (Test-Path $destino)) {
        Write-Host ("  FALTA el fichero $($atajo.Destino) en la raiz del proyecto.") -ForegroundColor Red
        continue
    }

    $ruta = Join-Path $escritorio ($atajo.Nombre + '.lnk')
    $enlace = $shell.CreateShortcut($ruta)
    $enlace.TargetPath = $destino
    # Sin esto el .cmd se ejecutaria desde System32 y no encontraria los .ps1.
    $enlace.WorkingDirectory = $raiz
    $enlace.Description = $atajo.Descripcion
    $enlace.IconLocation = $atajo.Icono
    $enlace.Save()

    Write-Host ("  Creado: " + $atajo.Nombre) -ForegroundColor Green
}

Write-Host ''
Write-Host '  Listo. Mira tu Escritorio: tienes tres iconos nuevos.' -ForegroundColor Green
Write-Host ''
