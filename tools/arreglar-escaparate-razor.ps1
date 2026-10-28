$ErrorActionPreference = 'Stop'
$f = 'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Pages\Escaparate.razor'
$l = [System.IO.File]::ReadAllLines($f)
if ($l.Count -ne 144) { throw "GUARDA: esperaba 144 lineas, hay $($l.Count)" }
if ($l[140].Trim() -ne '}') { throw "GUARDA: linea 141 no es '}' sino '$($l[140])'" }
if ($l[141].Trim() -ne '</div>') { throw "GUARDA: linea 142 no es '</div>' sino '$($l[141])'" }
if ($l[143].Trim() -ne '}') { throw "GUARDA: linea 144 no es '}' sino '$($l[143])'" }
$cola = @(
    '',
    '@if (SinConexion)',
    '{',
    '    @* Sin circuito la television se queda congelada: hay que avisar en sala. *@',
    '    <p class="tv-reconectando" role="status">',
    '        <IconoVino Nombre="Icono.Reloj" Tamano="TamanoIcono.Medio" />',
    '        Reconectando con la mesa...',
    '    </p>',
    '}'
)
$nuevo = $l[0..140] + $cola
[System.IO.File]::WriteAllLines($f, $nuevo)
Write-Output ("OK. Lineas: " + ([System.IO.File]::ReadAllLines($f)).Count)
