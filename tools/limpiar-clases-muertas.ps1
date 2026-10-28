$ErrorActionPreference = 'Stop'

# Elimina de Styles/estilos.css las tres clases mc-* que se anadieron para
# normalizar el espaciado de formularios pero que ningun .razor usa: Tailwind
# las purga en el build, asi que son codigo muerto que solo despista.
$f = 'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Styles\estilos.css'
$texto = [System.IO.File]::ReadAllText($f)
$largo = $texto.Length

$reglas = @(
    @{ n = 'mc-formulario'; p = '(?s)\r?\n\s*/\* Contenedor de un formulario completo.*?\.mc-formulario \{[^}]*\}\r?\n' }
    @{ n = 'mc-casilla';    p = '(?s)\r?\n\s*/\* Casilla o radio con su texto.*?\.mc-casilla \{[^}]*\}\r?\n' }
    @{ n = 'mc-acciones';   p = '(?s)\r?\n\s*/\* Fila de botones al pie.*?\.mc-acciones \{[^}]*\}\r?\n' }
)

foreach ($r in $reglas) {
    $n = [regex]::Matches($texto, $r.p).Count
    if ($n -ne 1) { throw ("Se esperaba 1 coincidencia de {0} y hay {1}" -f $r.n, $n) }
    $texto = [regex]::Replace($texto, $r.p, "`r`n")
    "{0,-16} eliminada" -f $r.n
}

$quitado = $largo - $texto.Length
if ($quitado -lt 300 -or $quitado -gt 1400) { throw "Recorte sospechoso: $quitado bytes" }

[System.IO.File]::WriteAllText($f, $texto)
"OK: $quitado bytes eliminados. Lineas finales: " + ([System.IO.File]::ReadAllLines($f)).Count
