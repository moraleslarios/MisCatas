#requires -Version 5.1
<#
    PARAR MISCATAS
    ==============

    Uso:
        .\Parar.ps1

    O directamente doble clic en  Parar.cmd

    Cierra el servidor de MisCatas y libera el puerto 5080.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

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
$puerto = @(Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue)

Write-Host ''
if ($quedan.Count -eq 0 -and $puerto.Count -eq 0) {
    Write-Host '  PARADA  ' -ForegroundColor Black -BackgroundColor Green -NoNewline
    Write-Host '  El puerto 5080 esta libre.' -ForegroundColor Cyan
    Write-Host ''
    exit 0
}

if ($quedan.Count -gt 0) {
    Write-Host '  ATENCION  ' -ForegroundColor White -BackgroundColor Red -NoNewline
    Write-Host "  Siguen vivos estos procesos: $($quedan.Id -join ', ')" -ForegroundColor Red
}
if ($puerto.Count -gt 0) {
    $pids = ($puerto | Select-Object -ExpandProperty OwningProcess -Unique) -join ', '
    Write-Host "  El puerto 5080 sigue ocupado por el PID: $pids" -ForegroundColor Yellow
    Write-Host '  (puede ser otro programa distinto de MisCatas)' -ForegroundColor DarkGray
}
Write-Host ''
exit 1
