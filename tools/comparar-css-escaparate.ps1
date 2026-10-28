# Compara los selectores del CSS fuente del escaparate con el CSS generado en
# el ultimo build, para saber exactamente que bloques se han perdido.
$ErrorActionPreference = 'Stop'
$raiz = 'C:\Git\Proyectos\MisCatas'
$fuente = Join-Path $raiz 'src\MisCatas.Web\Components\Pages\Escaparate.razor.css'
$gen = Join-Path $raiz 'src\MisCatas.Web\obj\Debug\net10.0\scopedcss\Components\Pages\Escaparate.razor.rz.scp.css'

function Clases([string]$ruta) {
    $texto = Get-Content -Raw -LiteralPath $ruta
    [regex]::Matches($texto, '\.([a-zA-Z][\w-]*)') |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique
}

$a = Clases $fuente
$b = Clases $gen

"FALTAN EN FUENTE:"
Compare-Object -ReferenceObject $a -DifferenceObject $b |
    Where-Object SideIndicator -eq '=>' |
    ForEach-Object { "  " + $_.InputObject }

"SOBRAN EN FUENTE (nuevas):"
Compare-Object -ReferenceObject $a -DifferenceObject $b |
    Where-Object SideIndicator -eq '<=' |
    ForEach-Object { "  " + $_.InputObject }
