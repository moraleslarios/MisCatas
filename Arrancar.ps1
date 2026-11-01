#requires -Version 5.1
<#
    ARRANCAR MISCATAS  (modo desarrollo)
    ====================================

    Uso:
        .\Arrancar.ps1                -> compila si hace falta, arranca y abre la web
        .\Arrancar.ps1 -Compilar      -> fuerza recompilar antes de arrancar
        .\Arrancar.ps1 -Https         -> abre el navegador por HTTPS (avisa del certificado)
        .\Arrancar.ps1 -Lan           -> abre la direccion de la red local (la de los moviles)
        .\Arrancar.ps1 -SinNavegador  -> no abre ningun navegador

    O directamente doble clic en  Arrancar.cmd

    La app escucha por los DOS puertos a la vez:
        http://localhost:5081    <- sin cifrar, y es el que conviene usar
        https://localhost:5443   <- cifrado, pero avisa de que no es de fiar

    Por defecto se abre el SIN CIFRAR. Parece al reves, pero es lo que hace que
    la web funcione en todas partes: el certificado lo firmamos nosotros, y el
    iPhone no solo desconfia, sino que ni siquiera deja aceptar el aviso. Usa
    -Https si quieres el cifrado en este ordenador.

    El log queda en  tools\salida-app.log
#>

[CmdletBinding()]
param(
    [switch]$Compilar,
    [switch]$SinNavegador,

    # Abre el navegador por el puerto cifrado. Da aviso de certificado y en
    # iPhone directamente no se puede entrar; esta aqui por si hace falta
    # comprobar algo que dependa de HTTPS.
    [switch]$Https,

    # Abre la direccion de la red local en lugar de localhost, para ver desde
    # el movil o la tele lo mismo que van a ver ellos.
    [switch]$Lan
)

$ErrorActionPreference = 'Stop'

$raiz = $PSScriptRoot
$proyecto = Join-Path $raiz 'src\MisCatas.Web\MisCatas.Web.csproj'
$exe = Join-Path $raiz 'src\MisCatas.Web\bin\Debug\net10.0\MisCatas.Web.exe'
$trabajo = Join-Path $raiz 'src\MisCatas.Web'
$log = Join-Path $raiz 'tools\salida-app.log'
$logError = Join-Path $raiz 'tools\salida-app.err.log'

# Los puertos viven en src\MisCatas.Web\Seguridad\PuertosApp.cs. Si cambian
# alli, hay que cambiarlos aqui tambien.
#
# El 5081 NO es el 5080: el 5080 lo ocupa IIS. Esta linea decia 5080 y por eso
# el panel de direcciones de aqui abajo marcaba el puerto como caido.
$puertoHttps = 5443
$puertoHttp = 5081

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

# Comprueba que un puerto esta de verdad aceptando conexiones. No usamos
# Invoke-WebRequest porque el certificado es propio del equipo y PowerShell 5.1
# lo rechaza; abrir un socket basta para saber que Kestrel esta escuchando.
function Test-PuertoEscuchando {
    param([int]$Puerto)
    try {
        $cliente = New-Object System.Net.Sockets.TcpClient
        $espera = $cliente.BeginConnect('127.0.0.1', $Puerto, $null, $null)
        $conectado = $espera.AsyncWaitHandle.WaitOne(2000, $false) -and $cliente.Connected
        $cliente.Close()
        return $conectado
    }
    catch { return $false }
}

Write-Host ''
if ($lista) {
    # Direccion de la red local: la que hay que usar desde los moviles y la tele.
    $ip = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
        Select-Object -First 1 -ExpandProperty IPAddress)

    # OJO: no llamar a estas variables $http / $https. PowerShell no distingue
    # mayusculas, asi que pisarian el parametro -Https y el navegador abriria
    # siempre la direccion equivocada.
    $escuchaHttps = Test-PuertoEscuchando -Puerto $puertoHttps
    $escuchaHttp = Test-PuertoEscuchando -Puerto $puertoHttp

    Write-Host '  LISTA  ' -ForegroundColor Black -BackgroundColor Green
    Write-Host ''
    Write-Host '  DIRECCIONES DISPONIBLES' -ForegroundColor White
    Write-Host '  -----------------------'

    $marcaHttp = if ($escuchaHttp) { 'OK' } else { 'NO' }
    $colorHttp = if ($escuchaHttp) { 'Green' } else { 'Red' }
    Write-Host "  [$marcaHttp] HTTP (recomendado)  : " -ForegroundColor $colorHttp -NoNewline
    Write-Host "http://localhost:$puertoHttp" -ForegroundColor Cyan

    $marcaHttps = if ($escuchaHttps) { 'OK' } else { 'NO' }
    $colorHttps = if ($escuchaHttps) { 'Green' } else { 'Red' }
    Write-Host "  [$marcaHttps] HTTPS (con aviso)   : " -ForegroundColor $colorHttps -NoNewline
    Write-Host "https://localhost:$puertoHttps" -ForegroundColor DarkCyan

    if ($ip) {
        Write-Host '  [OK] Moviles y tele      : ' -ForegroundColor Green -NoNewline
        Write-Host "http://${ip}:$puertoHttp" -ForegroundColor Cyan
        Write-Host "                             https://${ip}:$puertoHttps" -ForegroundColor DarkGray
    }

    Write-Host ''
    Write-Host '  Reparte la direccion HTTP: entra tal cual en cualquier aparato.' -ForegroundColor DarkGray
    Write-Host '  La de HTTPS usa un certificado propio de este equipo, asi que el' -ForegroundColor DarkGray
    Write-Host '  navegador avisa (y el iPhone no deja ni aceptar el aviso).' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host "  PID del proceso : $($proceso.Id)"
    Write-Host "  Log             : $log"
    Write-Host '  Para pararla    : .\Parar.ps1  (o doble clic en Parar.cmd)'
    Write-Host ''

    if (-not $SinNavegador) {
        # Por defecto el sin cifrar, que es el que funciona en todos los
        # aparatos. -Https abre el cifrado; -Lan usa la IP de la red.
        $maquina = if ($Lan -and $ip) { $ip } else { 'localhost' }
        $destino = if ($Https) { "https://${maquina}:$puertoHttps" } else { "http://${maquina}:$puertoHttp" }
        Write-Host "  Abriendo $destino ..." -ForegroundColor DarkGray
        Start-Process $destino
    }
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
