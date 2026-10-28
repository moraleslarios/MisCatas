# Elimina los bloques @media (prefers-color-scheme: dark) de las hojas de
# estilo. Motivo: el modo oscuro estaba implementado en solo 13 de 36 ficheros,
# pero :root invertia los colores de TEXTO de forma global. Resultado: en un
# movil con tema oscuro, los textos salian casi blancos (#E6DCDA) sobre las
# tarjetas, que siguen con "background: #fff" literal -> contraste 1.2:1,
# completamente ilegible. La identidad de MisCatas es crema/burdeos, asi que
# se fija el esquema claro y se retira el oscuro.
$ErrorActionPreference = 'Stop'

$ficheros = @(
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Pages\Admin\CataControl.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Pages\Admin\CataMontar.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Pages\Admin\Panel.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Pages\Catas\Resultado.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Pages\Catas\Resumen.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Pages\Catas\Votar.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Pages\Home.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Shared\CuentaAtras.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Shared\EstadoVacio.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Shared\ModalConfirmacion.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Shared\TarjetaCata.razor.css',
    'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Shared\VotoInput.razor.css'
)

foreach ($ruta in $ficheros) {
    if (-not (Test-Path $ruta)) { throw "No existe: $ruta" }
    $lineas = [System.IO.File]::ReadAllLines($ruta)
    $salida = New-Object System.Collections.Generic.List[string]
    $i = 0
    $bloques = 0

    while ($i -lt $lineas.Count) {
        if ($lineas[$i] -match '@media\s*\(prefers-color-scheme:\s*dark\)') {
            # Recorta tambien el comentario inmediatamente anterior si lo hay.
            while ($salida.Count -gt 0 -and $salida[$salida.Count - 1].Trim() -eq '') {
                $salida.RemoveAt($salida.Count - 1)
            }
            $j = $salida.Count - 1
            if ($j -ge 0 -and $salida[$j].Trim().EndsWith('*/')) {
                while ($j -ge 0 -and -not $salida[$j].Trim().StartsWith('/*')) { $j-- }
                if ($j -ge 0) { while ($salida.Count -gt $j) { $salida.RemoveAt($salida.Count - 1) } }
            }

            # Avanza hasta cerrar el bloque equilibrando llaves.
            $nivel = 0
            do {
                $nivel += ([regex]::Matches($lineas[$i], '\{')).Count
                $nivel -= ([regex]::Matches($lineas[$i], '\}')).Count
                $i++
            } while ($i -lt $lineas.Count -and $nivel -gt 0)
            if ($nivel -ne 0) { throw "Llaves desequilibradas en $ruta" }
            $bloques++
            continue
        }
        $salida.Add($lineas[$i])
        $i++
    }

    if ($bloques -ne 1) { throw "Se esperaba 1 bloque oscuro en $ruta, encontrados $bloques" }
    # Guarda: nunca debe quedar vacio ni perder mas de 60 lineas.
    if ($salida.Count -lt 5) { throw "Resultado sospechosamente corto en $ruta" }
    if (($lineas.Count - $salida.Count) -gt 80) {
        throw "Se habrian borrado $($lineas.Count - $salida.Count) lineas en $ruta. Abortado."
    }

    [System.IO.File]::WriteAllLines($ruta, $salida)
    "{0,-30} {1} -> {2} lineas" -f (Split-Path $ruta -Leaf), $lineas.Count, $salida.Count
}
"---"
"Bloques de modo oscuro eliminados en $($ficheros.Count) ficheros."
