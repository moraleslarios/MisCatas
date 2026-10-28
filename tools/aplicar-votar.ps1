$ErrorActionPreference = 'Stop'
$destino = 'C:\Git\Proyectos\MisCatas\src\MisCatas.Web\Components\Pages\Catas\Votar.razor.cs'
$origen  = "$destino.nuevo"
$texto = [System.IO.File]::ReadAllText($origen)
if ($texto -notmatch 'ClienteCataHub') { throw 'GUARDA: falta ClienteCataHub' }
if ($texto -notmatch 'IAsyncDisposable') { throw 'GUARDA: falta IAsyncDisposable' }
if ($texto -match 'private Timer') { throw 'GUARDA: sigue habiendo Timer' }
$n = [regex]::Matches($texto, 'partial class Votar').Count
if ($n -ne 1) { throw "GUARDA: partial class Votar aparece $n veces" }
[System.IO.File]::WriteAllText($destino, $texto)
Remove-Item $origen -ErrorAction Stop
Write-Output ("OK. Lineas: " + ([System.IO.File]::ReadAllLines($destino)).Count)
