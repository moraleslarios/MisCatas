# ---------------------------------------------------------------------------
#  Descarga las fuentes web de MisCatas y las deja AUTOALOJADAS en wwwroot.
#
#  La aplicación corre en una red local que puede no tener internet, así que
#  no se puede depender de un CDN. Este script se ejecuta UNA SOLA VEZ (o
#  cuando se quiera actualizar la versión de las fuentes) y deja en el repo:
#
#      wwwroot\fuentes\*.woff2      los ficheros de fuente
#      wwwroot\fuentes\fuentes.css  las reglas @font-face apuntando a ellos
#
#  Solo se baja el subconjunto que necesita el español (latin + latin-ext).
#
#  Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\descargar-fuentes.ps1
# ---------------------------------------------------------------------------

$ErrorActionPreference = 'Stop'

$raiz    = Split-Path -Parent $PSScriptRoot
$destino = Join-Path $raiz 'src\MisCatas.Web\wwwroot\fuentes'

# Google Fonts devuelve woff2 moderno solo si el navegador lo soporta.
$agente = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'

# Familias a descargar. 'Css' es la URL de la hoja de Google Fonts.
$familias = @(
    @{ Nombre = 'Playfair Display'; Prefijo = 'playfair-display'; Css = 'https://fonts.googleapis.com/css2?family=Playfair+Display:wght@400..700&display=swap' }
    @{ Nombre = 'Inter';            Prefijo = 'inter';            Css = 'https://fonts.googleapis.com/css2?family=Inter:wght@400..700&display=swap' }
)

# Solo estos subconjuntos: es lo que necesita el castellano.
$subconjuntosValidos = @('latin', 'latin-ext')

if (-not (Test-Path $destino)) {
    New-Item -ItemType Directory -Path $destino -Force | Out-Null
}

$bloques = New-Object System.Collections.Generic.List[string]
$bloques.Add('/* ---------------------------------------------------------------------------')
$bloques.Add('   Fuentes autoalojadas de MisCatas. FICHERO GENERADO: no editar a mano.')
$bloques.Add('   Lo produce tools\descargar-fuentes.ps1 (ver README).')
$bloques.Add('')
$bloques.Add('   Playfair Display  -> titulos (serif con caracter, aire de etiqueta de vino)')
$bloques.Add('   Inter             -> datos y texto corrido (maxima legibilidad)')
$bloques.Add('   --------------------------------------------------------------------------- */')
$bloques.Add('')

foreach ($familia in $familias) {

    Write-Host "Pidiendo la hoja de estilos de '$($familia.Nombre)'..." -ForegroundColor Cyan
    $hoja = (Invoke-WebRequest -Uri $familia.Css -UseBasicParsing -UserAgent $agente -TimeoutSec 60).Content

    # Cada @font-face va precedido por un comentario con el nombre del subconjunto.
    $patron = '/\*\s*(?<sub>[a-z\-]+)\s*\*/\s*(?<regla>@font-face\s*\{[^}]*\})'
    $trozos = [regex]::Matches($hoja, $patron, [System.Text.RegularExpressions.RegexOptions]::Singleline)

    $encontrados = 0

    foreach ($trozo in $trozos) {

        $sub = $trozo.Groups['sub'].Value
        if ($subconjuntosValidos -notcontains $sub) { continue }

        $regla = $trozo.Groups['regla'].Value

        $url = [regex]::Match($regla, "url\((?<u>https://[^)]+\.woff2)\)").Groups['u'].Value
        if ([string]::IsNullOrWhiteSpace($url)) { continue }

        $peso   = [regex]::Match($regla, "font-weight:\s*(?<p>[^;]+);").Groups['p'].Value.Trim()
        $estilo = [regex]::Match($regla, "font-style:\s*(?<e>[^;]+);").Groups['e'].Value.Trim()
        $rango  = [regex]::Match($regla, "unicode-range:\s*(?<r>[^;]+);").Groups['r'].Value.Trim()

        # Nombre de fichero estable y legible: inter-latin-400-700.woff2
        $pesoArchivo = ($peso -replace '\s+', '-')
        $archivo     = "$($familia.Prefijo)-$sub-$pesoArchivo.woff2"
        $rutaArchivo = Join-Path $destino $archivo

        Write-Host "  -> $archivo" -ForegroundColor DarkGray
        Invoke-WebRequest -Uri $url -OutFile $rutaArchivo -UseBasicParsing -UserAgent $agente -TimeoutSec 120

        $bloques.Add("/* $($familia.Nombre) - $sub */")
        $bloques.Add('@font-face {')
        $bloques.Add("    font-family: '$($familia.Nombre)';")
        $bloques.Add("    font-style: $estilo;")
        $bloques.Add("    font-weight: $peso;")
        $bloques.Add('    font-display: swap;')
        $bloques.Add("    src: url('./$archivo') format('woff2');")
        if ($rango) { $bloques.Add("    unicode-range: $rango;") }
        $bloques.Add('}')
        $bloques.Add('')

        $encontrados++
    }

    if ($encontrados -eq 0) {
        throw "No se ha podido extraer ningun @font-face de '$($familia.Nombre)'. Puede que Google Fonts haya cambiado el formato de respuesta."
    }
}

$rutaCss = Join-Path $destino 'fuentes.css'
# UTF-8 sin BOM: el BOM molesta a algunos parsers de CSS.
[System.IO.File]::WriteAllText($rutaCss, ($bloques -join "`r`n"), (New-Object System.Text.UTF8Encoding($false)))

$total = (Get-ChildItem -Path $destino -Filter '*.woff2' | Measure-Object -Property Length -Sum).Sum
Write-Host ''
Write-Host "Listo. Fuentes en: $destino" -ForegroundColor Green
Write-Host ("Ficheros .woff2: {0}  ({1:N0} KB en total)" -f (Get-ChildItem -Path $destino -Filter '*.woff2').Count, ($total / 1KB)) -ForegroundColor Green
Write-Host 'Las reglas @font-face estan en wwwroot\fuentes\fuentes.css' -ForegroundColor Green
