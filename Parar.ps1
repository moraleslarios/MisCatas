#requires -Version 5.1
<#
    PARAR MISCATAS
    ==============

    Uso:
        .\Parar.ps1

    O directamente doble clic en  Parar.cmd

    Cierra el servidor de MisCatas y libera los dos puertos:
        5081 -> HTTP, el que se usa desde los moviles y la tele
        5443 -> HTTPS, cifrado pero con aviso de certificado
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Los puertos viven en src\MisCatas.Web\Seguridad\PuertosApp.cs.
#
# El 5081 NO es el 5080, que es el de IIS. Aqui decia 5080 y por eso el aviso
# final daba por liberado un puerto que no era el de la app.
$puertoHttps = 5443
$puertoHttp = 5081

Write-Host ''
Write-Host '=== Parando MisCatas ===' -ForegroundColor Magenta
Write-Host ''

# Solo matamos MisCatas.Web. NUNCA "dotnet" a secas: se llevaria por delante
# procesos de Visual Studio y de las extensiones de VS Code.
$procesos = @(Get-Process -Name 'MisCatas.Web' -ErrorAction SilentlyContinue)

if ($procesos.Count -eq 0) {
    Write-Host '  No estaba corriendo. Nada que parar.' -ForegroundColor DarkGray
}
else {
    foreach ($p in $procesos) {
        Write-Host "  Cerrando PID $($p.Id)..." -ForegroundColor Yellow
        try { $p | Stop-Process -Force -ErrorAction Stop }
        catch { Write-Host "    No se pudo cerrar: $($_.Exception.Message)" -ForegroundColor Red }
    }
    Start-Sleep -Milliseconds 1500
}

# Comprobacion final
$quedan = @(Get-Process -Name 'MisCatas.Web' -ErrorAction SilentlyContinue)

# Los dos puertos: el seguro (HTTPS), que es el que atiende de verdad, y el
# viejo (HTTP), que solo esta para redirigir al seguro.
$puertos = @($puertoHttps, $puertoHttp)
$ocupados = @()
foreach ($p in $puertos) {
    if (Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue) {
        $ocupados += $p
    }
}
$puerto = $ocupados

Write-Host ''
if ($quedan.Count -eq 0 -and $puerto.Count -eq 0) {
    Write-Host '  PARADA  ' -ForegroundColor Black -BackgroundColor Green
    Write-Host ''
    Write-Host "  Puerto $puertoHttps (HTTPS) : libre" -ForegroundColor Cyan
    Write-Host "  Puerto $puertoHttp (HTTP)  : libre" -ForegroundColor Cyan
    Write-Host ''
    exit 0
}

if ($quedan.Count -gt 0) {
    Write-Host '  ATENCION  ' -ForegroundColor White -BackgroundColor Red -NoNewline
    Write-Host "  Siguen vivos estos procesos: $($quedan.Id -join ', ')" -ForegroundColor Red
}
if ($puerto.Count -gt 0) {
    foreach ($p in $puerto) {
        $que = if ($p -eq $puertoHttps) { 'HTTPS' } else { 'HTTP' }
        $con = @(Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue)
        $pids = ($con | Select-Object -ExpandProperty OwningProcess -Unique) -join ', '
        Write-Host "  El puerto $p ($que) sigue ocupado por el PID: $pids" -ForegroundColor Yellow
    }
    Write-Host '  (puede ser otro programa distinto de MisCatas)' -ForegroundColor DarkGray
}
Write-Host ''
exit 1
