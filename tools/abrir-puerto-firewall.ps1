# ---------------------------------------------------------------------------
# Abre el puerto 5080 en el Firewall de Windows para poder entrar en MisCatas
# desde otros dispositivos de la red local (moviles, tablets, otros PCs).
#
# HAY QUE EJECUTARLO COMO ADMINISTRADOR:
#   Boton derecho sobre PowerShell -> "Ejecutar como administrador", y luego:
#   powershell -NoProfile -ExecutionPolicy Bypass -File c:\Git\Proyectos\MisCatas\tools\abrir-puerto-firewall.ps1
# ---------------------------------------------------------------------------

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

$nombreRegla = 'MisCatas (HTTP 5080)'
$puerto = 5080

Write-Host ''
Write-Host '=== MisCatas: abrir puerto en el firewall ===' -ForegroundColor Cyan
Write-Host ''

# --- 1. Crear o actualizar la regla de entrada -----------------------------
$reglaExistente = Get-NetFirewallRule -DisplayName $nombreRegla -ErrorAction SilentlyContinue

if ($reglaExistente) {
    Write-Host "La regla '$nombreRegla' ya existe. Se elimina para recrearla limpia." -ForegroundColor Yellow
    Remove-NetFirewallRule -DisplayName $nombreRegla
}

New-NetFirewallRule `
    -DisplayName $nombreRegla `
    -Description 'Permite el acceso a la app MisCatas desde la red local.' `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort $puerto `
    -Profile Domain, Private, Public `
    -Enabled True | Out-Null

Write-Host "[OK] Regla creada: TCP $puerto permitido en entrada." -ForegroundColor Green

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
        Write-Host ("  http://{0}:{1}" -f $_.IPAddress, $puerto) -ForegroundColor White
        Write-Host ("     (interfaz: {0})" -f $_.InterfaceAlias) -ForegroundColor DarkGray
    }

Write-Host ''
Write-Host 'Recuerda: HTTP, no HTTPS. Y el movil en la MISMA red wifi que el PC.' -ForegroundColor Gray
Write-Host ''
