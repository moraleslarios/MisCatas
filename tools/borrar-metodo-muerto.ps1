# Elimina el metodo muerto ObtenerResultadoAntiguoAsync de ServicioParticipacion.cs
# (lineas 209 a 312 inclusive, 1-indexadas), sustituyendolo por nada.
$ruta = 'C:\Git\Proyectos\MisCatas\src\MisCatas.Infrastructure\Servicios\ServicioParticipacion.cs'
$lineas = [System.IO.File]::ReadAllLines($ruta)
Write-Host "Total lineas antes: $($lineas.Count)"
Write-Host "L209: $($lineas[208])"
Write-Host "L312: $($lineas[311])"
Write-Host "L313: $($lineas[312])"
if ($lineas[208] -notmatch 'ObtenerResultadoAntiguoAsync') { Write-Host 'ABORTADO: L209 no es el metodo esperado'; exit 1 }
if ($lineas[310].Trim() -ne '}') { Write-Host 'ABORTADO: L311 no es el cierre esperado'; exit 1 }
if ($lineas[311].Trim() -ne '') { Write-Host 'ABORTADO: L312 no esta en blanco'; exit 1 }
$nuevas = @()
$nuevas += $lineas[0..207]
$nuevas += $lineas[312..($lineas.Count - 1)]
[System.IO.File]::WriteAllLines($ruta, $nuevas)
Write-Host "Total lineas despues: $($nuevas.Count)"
