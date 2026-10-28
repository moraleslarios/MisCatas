# ---------------------------------------------------------------------------
# Oscurece los grises claros literales de los .razor.css aislados.
#
# Los CSS aislados de Blazor no pasan por Tailwind, asi que durante la fase 2
# se escribieron hexadecimales sueltos (#666, #777, #888, #8A7F82...) que
# sobre crema quedaban ilegibles. Este script los sustituye por las variables
# CSS --carbon-* definidas en Styles/estilos.css, con fallback al nuevo tono.
#
# Es idempotente: se puede volver a lanzar sin efectos secundarios.
# ---------------------------------------------------------------------------

$raiz = Split-Path -Parent $PSScriptRoot
$carpeta = Join-Path $raiz 'src\MisCatas.Web'

# El orden IMPORTA: primero los fallbacks ya existentes y los hexadecimales
# de 6 digitos, para que al tratar #666 no se destroce #666062.
$reglas = @(
    @{ Patron = 'var\(--carbon-400,\s*#8A8285\)'; Nuevo = 'var(--carbon-400, #786F72)' },
    @{ Patron = 'var\(--carbon-500,\s*#666062\)'; Nuevo = 'var(--carbon-600, #4A4446)' },
    @{ Patron = '#8A7F82';                        Nuevo = 'var(--carbon-500, #5E5659)' },
    @{ Patron = '#666062';                        Nuevo = 'var(--carbon-600, #4A4446)' },
    @{ Patron = '#[aA]08[cC]92';                  Nuevo = 'var(--carbon-500, #5E5659)' },
    @{ Patron = '#666(?![0-9A-Fa-f])';            Nuevo = 'var(--carbon-600, #4A4446)' },
    @{ Patron = '#777(?![0-9A-Fa-f])';            Nuevo = 'var(--carbon-600, #4A4446)' },
    @{ Patron = '#888(?![0-9A-Fa-f])';            Nuevo = 'var(--carbon-500, #5E5659)' },
    @{ Patron = '#999(?![0-9A-Fa-f])';            Nuevo = 'var(--carbon-500, #5E5659)' }
)

$tocados = 0
$fallidos = @()

foreach ($fichero in Get-ChildItem -Path $carpeta -Filter '*.razor.css' -Recurse) {
    $original = [System.IO.File]::ReadAllText($fichero.FullName)
    $texto = $original

    foreach ($regla in $reglas) {
        $texto = [System.Text.RegularExpressions.Regex]::Replace($texto, $regla.Patron, $regla.Nuevo)
    }

    if ($texto -ne $original) {
        try {
            [System.IO.File]::WriteAllText($fichero.FullName, $texto, (New-Object System.Text.UTF8Encoding $true))
            $tocados++
            Write-Host ("OK   {0}" -f $fichero.FullName.Substring($raiz.Length + 1))
        }
        catch {
            $fallidos += $fichero.FullName
            Write-Host ("FALLO {0} -> {1}" -f $fichero.Name, $_.Exception.Message)
        }
    }
}

Write-Host ""
Write-Host ("Ficheros modificados: {0}" -f $tocados)
Write-Host ("Ficheros con fallo:   {0}" -f $fallidos.Count)
