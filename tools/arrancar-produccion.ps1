# Arranca MisCatas lo más parecido posible a producción.
#
# Por qué publica en vez de hacer solo "dotnet run -c Release":
#   con ASPNETCORE_ENVIRONMENT=Production, "dotnet run" NO activa los static web
#   assets, así que blazor.web.js, MisCatas.Web.styles.css y los .razor.js dan
#   404: la web sale sin estilos y sin interactividad. Ejecutando la salida de
#   "dotnet publish" todos esos ficheros existen de verdad en wwwroot.
#
# Qué hace, en orden:
#   1. Para cualquier instancia anterior (si no, los DLL quedan bloqueados).
#   2. Publica en Release, lo que además regenera y minifica el CSS de Tailwind
#      (ver el target "CompilarTailwind" de MisCatas.Web.csproj).
#   3. Comprueba que están el CSS y los ficheros estáticos clave.
#   4. Arranca el .exe publicado con ASPNETCORE_ENVIRONMENT=Production y los
#      endpoints de appsettings.json (0.0.0.0:5080), accesible desde los móviles.
#
# Las migraciones de base de datos y la semilla las aplica la propia app al
# arrancar (Program.cs -> MigrarYSembrarAsync), así que no hay paso extra.

[CmdletBinding()]
param(
    # Omite la publicación y arranca lo ya publicado (más rápido).
    [switch]$SinPublicar,

    # No abre el navegador al terminar de arrancar.
    [switch]$SinNavegador
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$proyecto = Join-Path $raiz 'src\MisCatas.Web'
$destino = Join-Path $raiz 'publicado'
$exe = Join-Path $destino 'MisCatas.Web.exe'
$cssCompilado = Join-Path $destino 'wwwroot\css\estilos.css'

# El CSS sano ronda los 18 KB. Si Tailwind purga de más, baja a ~8 KB y la web
# se ve sin estilos: avisamos antes de arrancar en lugar de descubrirlo en el TV.
$cssMinimoBytes = 14000

Write-Host ''
Write-Host '=== MisCatas - arranque en modo produccion ===' -ForegroundColor Magenta
Write-Host ''

# --- 1. Parar la instancia anterior ----------------------------------------
# Solo matamos MisCatas.Web: un "Stop-Process -Name dotnet" se llevaría por
# delante procesos de Visual Studio y de las extensiones de VS Code.
$previos = @(Get-Process -Name 'MisCatas.Web' -ErrorAction SilentlyContinue)
if ($previos.Count -gt 0) {
    Write-Host "[1/4] Parando $($previos.Count) instancia(s) anterior(es)..." -ForegroundColor Yellow
    $previos | Stop-Process -Force
    Start-Sleep -Milliseconds 1500
}
else {
    Write-Host '[1/4] No habia instancias anteriores.' -ForegroundColor DarkGray
}

# Si el puerto sigue ocupado por otra cosa, mejor saberlo ya.
$ocupado = Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue
if ($ocupado) {
    $pids = ($ocupado.OwningProcess | Sort-Object -Unique) -join ', '
    throw "El puerto 5080 sigue ocupado por el/los proceso(s) $pids. Cierralo y vuelve a intentarlo."
}

# --- 2. Publicar en Release -------------------------------------------------
if ($SinPublicar) {
    Write-Host '[2/4] Publicacion omitida (-SinPublicar).' -ForegroundColor DarkGray
    if (-not (Test-Path $exe)) {
        throw "No existe $exe. Ejecuta el script sin -SinPublicar al menos una vez."
    }
}
else {
    Write-Host '[2/4] Publicando en Release (incluye Tailwind)...' -ForegroundColor Cyan
    & dotnet publish $proyecto -c Release -o $destino --nologo -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "La publicacion ha fallado (codigo $LASTEXITCODE). Revisa los errores de arriba."
    }
}

# --- 3. Comprobar el resultado ---------------------------------------------
if (-not (Test-Path $cssCompilado)) {
    throw "No existe $cssCompilado. Ejecuta tools\descargar-tailwind.ps1 y vuelve a publicar."
}

$bytesCss = (Get-Item $cssCompilado).Length
if ($bytesCss -lt $cssMinimoBytes) {
    throw "El CSS publicado solo tiene $bytesCss bytes (se esperaban mas de $cssMinimoBytes). Tailwind ha purgado de mas y la web se veria sin estilos."
}

# Estos tres son los que fallaban al arrancar sin publicar.
$criticos = @(
    'wwwroot\_framework\blazor.web.js',
    'wwwroot\MisCatas.Web.styles.css',
    'MisCatas.Web.exe'
)
foreach ($relativo in $criticos) {
    if (-not (Test-Path (Join-Path $destino $relativo))) {
        throw "Falta el fichero publicado '$relativo'. La publicacion esta incompleta."
    }
}
Write-Host "[3/4] Publicacion correcta: CSS $bytesCss bytes y estaticos presentes." -ForegroundColor Green

# --- 4. Arrancar ------------------------------------------------------------
$ip = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' } |
    Select-Object -First 1 -ExpandProperty IPAddress)

Write-Host '[4/4] Arrancando...' -ForegroundColor Cyan
Write-Host ''
Write-Host '  Este equipo : http://localhost:5080' -ForegroundColor White
if ($ip) {
    Write-Host "  Moviles/TVs : http://${ip}:5080" -ForegroundColor White
}
Write-Host '  Entorno     : Production (errores genericos, sin detalles tecnicos)' -ForegroundColor DarkGray
Write-Host ''
Write-Host '  Ctrl+C para parar.' -ForegroundColor DarkGray
Write-Host ''

if (-not $SinNavegador) {
    # Lanzamos el navegador en paralelo: espera a que Kestrel escuche y abre.
    Start-Job -ScriptBlock {
        for ($i = 0; $i -lt 60; $i++) {
            Start-Sleep -Milliseconds 500
            if (Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue) {
                # Kestrel escucha en 0.0.0.0, que no es una direccion navegable.
                Start-Process 'http://localhost:5080'
                return
            }
        }
    } | Out-Null
}

$env:ASPNETCORE_ENVIRONMENT = 'Production'
Push-Location $destino
try {
    & $exe
}
finally {
    Pop-Location
    Get-Job | Remove-Job -Force -ErrorAction SilentlyContinue
}
