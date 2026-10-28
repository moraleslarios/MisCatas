# ---------------------------------------------------------------------------
# Descarga el CLI standalone de Tailwind CSS en tools/tailwindcss.exe.
#
# MisCatas no necesita Node ni npm: el binario standalone de Tailwind hace
# todo el trabajo. El build del proyecto web lo invoca automáticamente.
#
# Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\descargar-tailwind.ps1
# ---------------------------------------------------------------------------

$ErrorActionPreference = 'Stop'

$version = 'v3.4.17'
$carpeta = $PSScriptRoot
$destino = Join-Path $carpeta 'tailwindcss.exe'
$url = "https://github.com/tailwindlabs/tailwindcss/releases/download/$version/tailwindcss-windows-x64.exe"

if (Test-Path $destino) {
    Write-Host "Ya existe $destino. Bórralo si quieres volver a descargarlo." -ForegroundColor Yellow
    & $destino --help | Select-Object -First 2
    return
}

Write-Host "Descargando Tailwind CSS $version..." -ForegroundColor Cyan
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri $url -OutFile $destino -UseBasicParsing

$mb = [math]::Round((Get-Item $destino).Length / 1MB, 1)
Write-Host "Listo: $destino ($mb MB)" -ForegroundColor Green
& $destino --help | Select-Object -First 2
