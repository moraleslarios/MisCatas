# Normaliza padding, gap y contraste de bordes en los formularios y listados.
# Los cuatro formularios de administracion (UsuarioEditar, VinoEditar,
# CataEditar, Ajustes) y las pantallas de cuenta comparten la misma estructura,
# asi que un unico juego de reglas los deja a todos coherentes.
$ErrorActionPreference = 'Stop'

$raiz = 'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components'

$reglas = @(
    # --- Contenedores de formulario: mas aire entre bloques ---------------
    @{ p = '(\.formulario\s*\{[^}]*?)gap:\s*1rem;'; r = '${1}gap: 1.375rem;' },
    @{ p = '(\.formulario\s*\{[^}]*?)padding:\s*clamp\(0\.9rem,\s*4vw,\s*1\.25rem\);'; r = '${1}padding: clamp(1.125rem, 4vw, 1.625rem);' },

    # --- Campo: etiqueta, control y ayuda separados -----------------------
    @{ p = '(\.campo\s*\{[^}]*?)gap:\s*0\.35rem;'; r = '${1}gap: 0.4375rem;' },

    # --- Controles: relleno interior comodo y borde visible ---------------
    @{ p = 'padding:\s*0\.55rem\s+0\.7rem;'; r = 'padding: 0.6875rem 0.75rem;' },
    @{ p = 'padding:\s*0\.6rem\s+0\.7rem;'; r = 'padding: 0.6875rem 0.75rem;' },
    @{ p = 'border:\s*1px\s+solid\s+#cfcfcf;'; r = 'border: 1px solid #B9ADA2;' },
    @{ p = 'border:\s*1px\s+solid\s+#e3e3e3;'; r = 'border: 1px solid var(--crema-borde, #DFD4C6);' },
    @{ p = 'border:\s*1px\s+solid\s+#ddd;'; r = 'border: 1px solid var(--crema-borde, #DFD4C6);' },

    # --- Casillas / interruptores: zona tactil real -----------------------
    @{ p = '(\.interruptor\s*\{\s*display:\s*flex;\s*)align-items:\s*center;(\s*)gap:\s*0\.5rem;'; r = '${1}align-items: flex-start;${2}gap: 0.6875rem;${2}padding: 0.5rem 0;${2}line-height: 1.4;' },
    @{ p = '(\.interruptor\s*::deep\s+input\s*\{[^}]*?)width:\s*1\.15rem;(\s*)height:\s*1\.15rem;'; r = '${1}width: 1.25rem;${2}height: 1.25rem;${2}margin: 0.0625rem 0 0;' },

    # --- Fila de botones: separacion del bloque anterior ------------------
    @{ p = '(\.botones\s*\{[^}]*?)gap:\s*0\.6rem;(\s*)margin-top:\s*0\.5rem;'; r = '${1}gap: 0.75rem;${2}margin-top: 0.75rem;${2}padding-top: 0.25rem;' },
    @{ p = 'padding:\s*0\.6rem\s+1\.1rem;'; r = 'padding: 0.75rem 1.25rem;' },

    # --- Movil: 48 px de alto tactil --------------------------------------
    @{ p = 'min-height:\s*2\.75rem;'; r = 'min-height: 3rem;' }
)

$total = 0
foreach ($fichero in Get-ChildItem $raiz -Recurse -Filter *.razor.css) {
    $texto = [System.IO.File]::ReadAllText($fichero.FullName)
    $largo = $texto.Length
    $cambios = 0
    foreach ($regla in $reglas) {
        $n = [regex]::Matches($texto, $regla.p).Count
        if ($n -gt 0) {
            $texto = [regex]::Replace($texto, $regla.p, $regla.r)
            $cambios += $n
        }
    }
    if ($cambios -gt 0) {
        if ($texto.Length -lt ($largo - 10)) { throw "Perdida de contenido en $($fichero.Name). Abortado." }
        [System.IO.File]::WriteAllText($fichero.FullName, $texto)
        "{0,-32} {1} cambio(s)" -f $fichero.Name, $cambios
        $total += $cambios
    }
}
"---"
"TOTAL: $total"
