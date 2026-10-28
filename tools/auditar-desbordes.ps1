# Audita los CSS aislados (.razor.css) buscando riesgo de desbordamiento de texto.
# Un contenedor flex/grid cuyos hijos no declaran min-width:0 provoca que las
# etiquetas largas se salgan del panel. Tambien detecta ausencia de overflow-wrap.

$raiz = Split-Path -Parent $PSScriptRoot
$ficheros = Get-ChildItem -Path (Join-Path $raiz 'src\MisCatas.Web') -Recurse -Filter '*.razor.css'

$informe = foreach ($f in $ficheros) {
    $texto = [System.IO.File]::ReadAllText($f.FullName)
    [pscustomobject]@{
        Fichero   = $f.FullName.Replace($raiz + '\', '')
        Flex      = ([regex]::Matches($texto, 'display:\s*(flex|grid)')).Count
        MinWidth  = ([regex]::Matches($texto, 'min-width:\s*0')).Count
        Wrap      = ([regex]::Matches($texto, 'overflow-wrap|word-break|hyphens')).Count
        Truncado  = ([regex]::Matches($texto, 'white-space:\s*nowrap')).Count
        Riesgo    = ''
    }
}

foreach ($r in $informe) {
    $m = @()
    if ($r.Flex -gt 0 -and $r.MinWidth -eq 0) { $m += 'falta-min-width' }
    if ($r.Truncado -gt 0) { $m += 'nowrap' }
    if ($r.Flex -gt 0 -and $r.Wrap -eq 0) { $m += 'falta-wrap' }
    $r.Riesgo = if ($m.Count -eq 0) { 'ok' } else { ($m -join ',') }
}

$informe | Where-Object { $_.Riesgo -ne 'ok' } |
    Sort-Object Riesgo, Fichero |
    Format-Table -AutoSize Riesgo, Flex, Truncado, Fichero

Write-Host ''
Write-Host ("Total ficheros: {0} | Con riesgo: {1}" -f $informe.Count, (($informe | Where-Object { $_.Riesgo -ne 'ok' }).Count))
