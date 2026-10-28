# Sincroniza la escala de grises nueva en todos los CSS aislados y sustituye
# los literales grises medios (#444, #555, #333...) por la variable adecuada.
$ErrorActionPreference = 'Stop'

$raiz = 'C:\Git\Proyectos\MisCatas\src\MisCatas.Web'

# Pares: patron regex -> reemplazo
$reglas = @(
    @{ p = '--carbon-800,\s*#2A2628'; r = '--carbon-800, #211D1F' },
    @{ p = '--carbon-700,\s*#3A3537'; r = '--carbon-700, #2B2628' },
    @{ p = '--carbon-600,\s*#4A4446'; r = '--carbon-600, #383133' },
    @{ p = '--carbon-500,\s*#5E5659'; r = '--carbon-500, #4A4245' },
    @{ p = '--carbon-400,\s*#786F72'; r = '--carbon-400, #5C5457' },
    @{ p = '(?<![a-zA-Z-])color:\s*#4a4446;'; r = 'color: var(--carbon-600, #383133);' },
    @{ p = '(?<![a-zA-Z-])color:\s*#444;'; r = 'color: var(--carbon-600, #383133);' },
    @{ p = '(?<![a-zA-Z-])color:\s*#555;'; r = 'color: var(--carbon-600, #383133);' },
    @{ p = '(?<![a-zA-Z-])color:\s*#333;'; r = 'color: var(--carbon-700, #2B2628);' },
    @{ p = '(?<![a-zA-Z-])color:\s*#3A3A3A;'; r = 'color: var(--carbon-700, #2B2628);' },
    @{ p = 'color:\s*rgb\(253 251 248 / 78%\)'; r = 'color: rgb(253 251 248 / 92%)' },
    @{ p = 'color:\s*rgb\(253 251 248 / 82%\)'; r = 'color: rgb(253 251 248 / 94%)' },
    @{ p = 'color:\s*rgb\(247 242 236 / 58%\)'; r = 'color: rgb(247 242 236 / 80%)' },
    @{ p = 'color:\s*rgb\(247 242 236 / 60%\)'; r = 'color: rgb(247 242 236 / 80%)' },
    @{ p = 'color:\s*rgb\(247 242 236 / 62%\)'; r = 'color: rgb(247 242 236 / 82%)' },
    @{ p = 'color:\s*rgb\(247 242 236 / 70%\)'; r = 'color: rgb(247 242 236 / 86%)' },
    @{ p = 'color:\s*rgb\(247 242 236 / 72%\)'; r = 'color: rgb(247 242 236 / 88%)' },
    @{ p = 'color:\s*rgb\(247 242 236 / 78%\)'; r = 'color: rgb(247 242 236 / 90%)' }
)

$total = 0
foreach ($fichero in Get-ChildItem $raiz -Recurse -Filter *.razor.css) {
    $texto = [System.IO.File]::ReadAllText($fichero.FullName)
    $largoOriginal = $texto.Length
    $cambios = 0
    foreach ($regla in $reglas) {
        $nuevo = [regex]::Replace($texto, $regla.p, $regla.r)
        if ($nuevo -ne $texto) {
            $cambios += [regex]::Matches($texto, $regla.p).Count
            $texto = $nuevo
        }
    }
    if ($cambios -gt 0) {
        # Guarda: el fichero nunca debe perder contenido.
        if ($texto.Length -lt ($largoOriginal - 10)) {
            throw "El fichero $($fichero.Name) habria perdido contenido. Abortado."
        }
        [System.IO.File]::WriteAllText($fichero.FullName, $texto)
        "{0,-34} {1} cambio(s)" -f $fichero.Name, $cambios
        $total += $cambios
    }
}
"---"
"TOTAL de sustituciones: $total"
