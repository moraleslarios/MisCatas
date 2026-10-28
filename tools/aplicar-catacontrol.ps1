$proyecto = 'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Pages\Admin'
$nuevo = Join-Path $proyecto 'CataControl.razor.cs.nuevo'
$destino = Join-Path $proyecto 'CataControl.razor.cs'

if (-not (Test-Path $nuevo)) { Write-Host 'FALTA el fichero nuevo'; exit 1 }

$contenido = [System.IO.File]::ReadAllText($nuevo)

if ($contenido -notmatch 'ClienteCataHub Vivo') { Write-Host 'GUARDA: el nuevo no contiene el cliente en vivo'; exit 1 }
if ($contenido -notmatch 'PedirResetearVino') { Write-Host 'GUARDA: el nuevo no contiene el mando'; exit 1 }

[System.IO.File]::WriteAllText($destino, $contenido)
Remove-Item $nuevo -ErrorAction Stop

Write-Host ('OK. Lineas: ' + ([System.IO.File]::ReadAllLines($destino)).Count)
