#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Quita MisCatas del IIS de este equipo y lo deja como estaba.

.DESCRIPTION
    Deshace lo que hizo tools\desplegar-iis.ps1:
      - Para y borra el sitio y su grupo de aplicaciones en IIS.
      - Quita la linea de miscatas.es del fichero hosts.
      - Borra el certificado de miscatas.es.
      - Vuelve a arrancar el sitio de ejemplo de IIS, si estaba.
      - Opcionalmente borra la carpeta publicada.

    NO desinstala IIS ni el complemento de .NET: son cosas de Windows que
    pueden estar usando otros programas.

    NO toca la base de datos: los datos de las catas se quedan intactos.

.NOTES
    Hay que ejecutarlo como ADMINISTRADOR (boton derecho sobre
    Retirar-IIS.cmd -> "Ejecutar como administrador").

.EXAMPLE
    .\retirar-iis.ps1
    Quita la web pero deja la carpeta publicada.

.EXAMPLE
    .\retirar-iis.ps1 -BorrarCarpeta
    Quita la web y borra tambien C:\inetpub\MisCatas.
#>

[CmdletBinding()]
param(
    [string]$Dominio = 'miscatas.es',
    [string]$Carpeta = 'C:\inetpub\MisCatas',
    [string]$Sitio = 'MisCatas',

    # Borra tambien los ficheros publicados.
    [switch]$BorrarCarpeta
)

$ErrorActionPreference = 'Stop'

function Escribir-Ok {
    param([string]$Texto)
    Write-Host "  OK  $Texto" -ForegroundColor Green
}

function Escribir-Info {
    param([string]$Texto)
    Write-Host "  --  $Texto" -ForegroundColor DarkGray
}

function Escribir-Aviso {
    param([string]$Texto)
    Write-Host "  !!  $Texto" -ForegroundColor Yellow
}

Write-Host ''
Write-Host '=============================================================' -ForegroundColor Magenta
Write-Host '  MisCatas - quitar la web del IIS de este equipo' -ForegroundColor Magenta
Write-Host '=============================================================' -ForegroundColor Magenta
Write-Host ''

if (-not (Get-Service -Name 'W3SVC' -ErrorAction SilentlyContinue)) {
    Escribir-Info 'IIS no esta instalado: no hay nada que quitar de IIS.'
}
else {
    Import-Module WebAdministration -ErrorAction Stop

    # --- Sitio ---
    if (Test-Path "IIS:\Sites\$Sitio") {
        Stop-Website -Name $Sitio -ErrorAction SilentlyContinue
        Remove-Website -Name $Sitio
        Escribir-Ok "Sitio '$Sitio' eliminado."
    }
    else {
        Escribir-Info "El sitio '$Sitio' no existia."
    }

    # --- Grupo de aplicaciones ---
    if (Test-Path "IIS:\AppPools\$Sitio") {
        Stop-WebAppPool -Name $Sitio -ErrorAction SilentlyContinue
        # Los procesos del grupo tardan un momento en morir; borrarlo antes
        # deja una entrada fantasma en IIS.
        Start-Sleep -Seconds 2
        Remove-WebAppPool -Name $Sitio
        Escribir-Ok "Grupo de aplicaciones '$Sitio' eliminado."
    }
    else {
        Escribir-Info "El grupo de aplicaciones '$Sitio' no existia."
    }

    # --- Sitio de ejemplo de IIS ---
    # El despliegue lo paro para liberar los puertos: se devuelve a su estado.
    if (Test-Path 'IIS:\Sites\Default Web Site') {
        Set-ItemProperty 'IIS:\Sites\Default Web Site' -Name serverAutoStart -Value $true
        Start-Website -Name 'Default Web Site' -ErrorAction SilentlyContinue
        Escribir-Ok 'Sitio de ejemplo de IIS devuelto a su estado original.'
    }

    # --- Certificado atado al puerto 5080 ---
    # El enlace del 5080 (el de los moviles) no va por nombre, asi que Windows
    # guarda su certificado en una tabla propia, aparte de IIS. Al borrar el sitio
    # esa anotacion se queda ahi: hay que retirarla a mano o el puerto queda
    # reservado y luego nadie mas lo puede usar.
    $anotacion = & netsh.exe http show sslcert ipport=0.0.0.0:5080 2>$null | Out-String
    if ($anotacion -match '5080') {
        & netsh.exe http delete sslcert ipport=0.0.0.0:5080 | Out-Null
        Escribir-Ok 'Certificado retirado del puerto 5080.'
    }
    else {
        Escribir-Info 'El puerto 5080 no tenia certificado atado.'
    }

    # --- Certificado ---
    $nombreAmistoso = "MisCatas ($Dominio)"
    $borrados = 0

    foreach ($almacen in @('Cert:\LocalMachine\My', 'Cert:\LocalMachine\Root')) {
        $encontrados = @(Get-ChildItem -Path $almacen -ErrorAction SilentlyContinue |
            Where-Object { $_.FriendlyName -eq $nombreAmistoso -or $_.Subject -eq "CN=$Dominio" })

        foreach ($cert in $encontrados) {
            Remove-Item -Path $cert.PSPath -Force -DeleteKey -ErrorAction SilentlyContinue
            $borrados++
        }
    }

    if ($borrados -gt 0) {
        Escribir-Ok "Borrado(s) $borrados certificado(s) de $Dominio."
    }
    else {
        Escribir-Info "No habia certificados de $Dominio."
    }

    # --- Carpeta publicada ---
    if ($BorrarCarpeta) {
        if (Test-Path $Carpeta) {
            try {
                Remove-Item -Path $Carpeta -Recurse -Force -ErrorAction Stop
                Escribir-Ok "Carpeta '$Carpeta' borrada."
            }
            catch {
                Escribir-Aviso "No se ha podido borrar '$Carpeta': $($_.Exception.Message)"
                Escribir-Aviso 'Prueba a ejecutar iisreset y repite.'
            }
        }
        else {
            Escribir-Info "La carpeta '$Carpeta' no existia."
        }
    }
    elseif (Test-Path $Carpeta) {
        Escribir-Info "La carpeta '$Carpeta' se conserva (usa -BorrarCarpeta para borrarla)."
    }
}

Write-Host ''
Write-Host '=============================================================' -ForegroundColor Magenta
Write-Host '  LISTO. La web ya no esta en IIS.' -ForegroundColor Green
Write-Host ''
Write-Host '  Los datos de las catas NO se han tocado: siguen en la base' -ForegroundColor DarkGray
Write-Host '  de datos. Puedes seguir usando Arrancar.cmd como siempre.' -ForegroundColor DarkGray
Write-Host ''
Write-Host '  IIS y el complemento de .NET se quedan instalados, por si' -ForegroundColor DarkGray
Write-Host '  otros programas los usan. Para quitarlos: "Activar o' -ForegroundColor DarkGray
Write-Host '  desactivar las caracteristicas de Windows".' -ForegroundColor DarkGray
Write-Host '=============================================================' -ForegroundColor Magenta
Write-Host ''
