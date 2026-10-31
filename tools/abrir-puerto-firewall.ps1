# ---------------------------------------------------------------------------
# Abre los puertos de MisCatas en el Firewall de Windows para poder entrar
# desde otros dispositivos de la red local (moviles, tablets, otros PCs).
#
#   5443 -> el puerto que usa la app cuando se arranca a mano (Arrancar.cmd)
#   5080 -> el puerto por el que entran el movil y el televisor cuando la web
#           la sirve el IIS. Tambien va cifrado.
#
# El nombre de las reglas dice "HTTPS"/"HTTP" por como nacieron; lo que importa
# es el numero del puerto. tools\desplegar-iis.ps1 crea la del 5080 por su
# cuenta, asi que este fichero solo hace falta si algo se ha quedado a medias.
#
# HAY QUE EJECUTARLO COMO ADMINISTRADOR:
#   Boton derecho sobre PowerShell -> "Ejecutar como administrador", y luego:
#   powershell -NoProfile -ExecutionPolicy Bypass -File c:\Git\Proyectos\MisCatas\tools\abrir-puerto-firewall.ps1
# ---------------------------------------------------------------------------

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

$reglas = @(
    @{ Nombre = 'MisCatas (HTTPS 5443)'; Puerto = 5443 }
    @{ Nombre = 'MisCatas (HTTP 5080)'; Puerto = 5080 }
)
$puerto = 5443

Write-Host ''
Write-Host '=== MisCatas: abrir puertos en el firewall ===' -ForegroundColor Cyan
Write-Host ''

# --- 1. Crear o actualizar las reglas de entrada ---------------------------
foreach ($regla in $reglas) {
    $existente = Get-NetFirewallRule -DisplayName $regla.Nombre -ErrorAction SilentlyContinue

    if ($existente) {
        Write-Host "La regla '$($regla.Nombre)' ya existe. Se elimina para recrearla limpia." -ForegroundColor Yellow
        Remove-NetFirewallRule -DisplayName $regla.Nombre
    }

    New-NetFirewallRule `
        -DisplayName $regla.Nombre `
        -Description 'Permite el acceso a la app MisCatas desde la red local.' `
        -Direction Inbound `
        -Action Allow `
        -Protocol TCP `
        -LocalPort $regla.Puerto `
        -Profile Domain, Private, Public `
        -Enabled True | Out-Null

    Write-Host "[OK] Regla creada: TCP $($regla.Puerto) permitido en entrada." -ForegroundColor Green
}

# --- 2. Avisar si la red esta marcada como publica -------------------------
# En redes "Public" Windows es mas restrictivo. La regla de arriba ya cubre
# ese perfil, pero marcar tu red de casa como privada es lo correcto y evita
# otros sustos (descubrimiento de red, comparticion, etc.).
$perfilesPublicos = Get-NetConnectionProfile | Where-Object { $_.NetworkCategory -eq 'Public' }

if ($perfilesPublicos) {
    Write-Host ''
    Write-Host 'AVISO: estas interfaces estan marcadas como red PUBLICA:' -ForegroundColor Yellow
    $perfilesPublicos | ForEach-Object { Write-Host "  - $($_.InterfaceAlias) ($($_.Name))" }
    Write-Host ''
    Write-Host 'Si es tu red de casa, conviene pasarla a privada. Para hacerlo ahora:' -ForegroundColor Yellow

    $respuesta = Read-Host '  Cambiar a red privada? (S/N)'

    if ($respuesta -match '^[SsYy]') {
        $perfilesPublicos | Set-NetConnectionProfile -NetworkCategory Private
        Write-Host '[OK] Red marcada como privada.' -ForegroundColor Green
    }
    else {
        Write-Host 'Se deja como publica. La regla del firewall funciona igualmente.' -ForegroundColor Gray
    }
}

# --- 3. Mostrar las direcciones utiles -------------------------------------
Write-Host ''
Write-Host '=== Direcciones para el navegador del movil ===' -ForegroundColor Cyan
Write-Host ''

Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
    ForEach-Object {
        Write-Host ("  https://{0}:{1}" -f $_.IPAddress, $puerto) -ForegroundColor White
        Write-Host ("     (interfaz: {0})" -f $_.InterfaceAlias) -ForegroundColor DarkGray
    }

Write-Host ''
Write-Host 'Recuerda: el movil en la MISMA red wifi que el PC.' -ForegroundColor Gray
Write-Host 'La primera vez el navegador avisara de que el certificado no es de' -ForegroundColor Gray
Write-Host 'fiar (es propio, de casa): acepta y continua. Solo pasa una vez por' -ForegroundColor Gray
Write-Host 'dispositivo.' -ForegroundColor Gray
Write-Host ''
