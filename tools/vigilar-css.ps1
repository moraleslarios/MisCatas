# ---------------------------------------------------------------------------
# Compila el CSS de Tailwind en modo vigilancia (--watch).
#
# Útil mientras se tocan estilos: cada vez que guardas un .razor o el CSS
# fuente, regenera wwwroot/css/estilos.css al instante. Déjalo corriendo en
# una terminal aparte junto a `dotnet watch`.
#
# Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\vigilar-css.ps1
# ---------------------------------------------------------------------------

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $PSScriptRoot 'tailwindcss.exe'

if (-not (Test-Path $exe)) {
    Write-Host 'Falta tools\tailwindcss.exe. Ejecuta primero tools\descargar-tailwind.ps1.' -ForegroundColor Red
    exit 1
}

# Los globos de `content` de tailwind.config.js son relativos a la raíz del
# repositorio, así que hay que lanzar el CLI desde allí o purgaría todo.
Push-Location $raiz

& $exe `
    -c (Join-Path $raiz 'tailwind.config.js') `
    -i (Join-Path $raiz 'src\MisCatas.Web\Styles\estilos.css') `
    -o (Join-Path $raiz 'src\MisCatas.Web\wwwroot\css\estilos.css') `
    --watch

Pop-Location
