#requires -Version 5.1
<#
    ARRANCAR MISCATAS  (modo desarrollo)
    ====================================

    Uso:
        .\Arrancar.ps1              -> compila si hace falta y arranca
        .\Arrancar.ps1 -Compilar    -> fuerza recompilar antes de arrancar
        .\Arrancar.ps1 -SinNavegador -> no abre el navegador

    O directamente doble clic en  Arrancar.cmd

    La app queda en  http://localhost:5080
    El log queda en  tools\salida-app.log
#>

[CmdletBinding()]
param(
    [switch]$Compilar,
    [switch]$SinNavegador
)

$ErrorActionPreference = 'Stop'

$raiz = $PSScriptRoot
$proyecto = Join-Path $raiz 'src\MisCatas.Web\MisCatas.Web.csproj'
$exe = Join-Path $raiz 'src\MisCatas.Web\bin\Debug\net10.0\MisCatas.Web.exe'
$trabajo = Join-Path $raiz 'src\MisCatas.Web'
$log = Join-Path $raiz 'tools\salida-app.log'
$logError = Join-Path $raiz 'tools\salida-app.err.log'
$url = 'http://localhost:5080'

Write-Host ''
Write-Host '=== Arrancando MisCatas ===' -ForegroundColor Magenta
Write-Host ''

# --- 1. Parar lo que hubiera corriendo -------------------------------------
# Solo matamos MisCatas.Web. NUNCA "dotnet" a secas: se llevaria por delante
# procesos de Visual Studio y de las extensiones de VS Code.
$previos = @(Get-Process -Name 'MisCatas.Web' -ErrorAction SilentlyContinue)
if ($previos.Count -gt 0) {
    Write-Host "[1/4] Parando $($previos.Count) instancia(s) anterior(es)..." -ForegroundColor Yellow
    $previos | Stop-Process -Force
    Start-Sleep -Milliseconds 1500
}
else {
    Write-Host '[1/4] No habia ninguna instancia corriendo.' -ForegroundColor DarkGray
}

# --- 2. Compilar si procede ------------------------------------------------
if ($Compilar -or -not (Test-Path $exe)) {
    Write-Host '[2/4] Compilando (esto tarda un poco)...' -ForegroundColor Yellow

    # El build tambien regenera el CSS de Tailwind (target CompilarTailwind del csproj).
    $salida = & dotnet build $proyecto 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host ''
        Write-Host 'ERROR al compilar. Estos son los fallos:' -ForegroundColor Red
        $salida | Select-String -Pattern ': error' | Select-Object -Unique -First 20 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        Write-Host ''
        Read-Host 'Pulsa Enter para cerrar'
        exit 1
    }
    Write-Host '      Compilacion correcta.' -ForegroundColor Green
}
else {
    Write-Host '[2/4] Ya estaba compilado (usa -Compilar para forzar).' -ForegroundColor DarkGray
}

# --- 3. Arrancar -----------------------------------------------------------
Write-Host '[3/4] Arrancando el servidor...' -ForegroundColor Yellow

if (Test-Path $log) { Remove-Item $log -Force -ErrorAction SilentlyContinue }
if (Test-Path $logError) { Remove-Item $logError -Force -ErrorAction SilentlyContinue }

# IMPRESCINDIBLE: al lanzar el .exe directamente no se lee launchSettings.json,
# asi que sin esto la app arranca en Production y NO sirve los CSS aislados de
# los componentes (MisCatas.Web.styles.css da 500 y la web sale sin formato).
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'

$proceso = Start-Process -FilePath $exe `
    -WorkingDirectory $trabajo `
    -RedirectStandardOutput $log `
    -RedirectStandardError $logError `
    -WindowStyle Hidden `
    -PassThru

# --- 4. Esperar a que este lista ------------------------------------------
Write-Host '[4/4] Esperando a que responda...' -ForegroundColor Yellow

$lista = $false
$limite = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $limite) {
    if ($proceso.HasExited) { break }
    if ((Test-Path $log) -and (Select-String -Path $log -Pattern 'Application started' -Quiet -ErrorAction SilentlyContinue)) {
        $lista = $true
        break
    }
    Start-Sleep -Milliseconds 500
}

Write-Host ''
if ($lista) {
    Write-Host '  LISTA  ' -ForegroundColor Black -BackgroundColor Green -NoNewline
    Write-Host "  ->  $url" -ForegroundColor Cyan
    Write-Host ''
    Write-Host "  PID del proceso : $($proceso.Id)"
    Write-Host "  Log             : $log"
    Write-Host "  Para pararla    : .\Parar.ps1  (o doble clic en Parar.cmd)"
    Write-Host ''
    if (-not $SinNavegador) { Start-Process $url }
    exit 0
}

Write-Host '  NO ARRANCO  ' -ForegroundColor White -BackgroundColor Red
Write-Host ''
Write-Host 'Ultimas lineas del log:' -ForegroundColor Yellow
if (Test-Path $log) { Get-Content $log -Tail 15 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray } }
if ((Test-Path $logError) -and (Get-Item $logError).Length -gt 0) {
    Write-Host 'Errores:' -ForegroundColor Yellow
    Get-Content $logError -Tail 15 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
}
Write-Host ''
Read-Host 'Pulsa Enter para cerrar'
exit 1
