# Compara la Foto y la Etiqueta de cada vino con un hash perceptual sencillo
# (reducción a 8x8 en gris + distancia media). Sirve para saber si en los dos
# huecos se subió la misma imagen o imágenes distintas.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\comparar-imagenes-vinos.ps1

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.Drawing

function ObtenerHuella([byte[]]$bytes)
{
    if ($null -eq $bytes) { return $null }

    $memoria = New-Object System.IO.MemoryStream(, $bytes)
    $original = [System.Drawing.Image]::FromStream($memoria)
    $pequena = New-Object System.Drawing.Bitmap 8, 8
    $lienzo = [System.Drawing.Graphics]::FromImage($pequena)
    $lienzo.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $lienzo.DrawImage($original, 0, 0, 8, 8)

    $huella = New-Object 'System.Collections.Generic.List[double]'
    for ($y = 0; $y -lt 8; $y++)
    {
        for ($x = 0; $x -lt 8; $x++)
        {
            $p = $pequena.GetPixel($x, $y)
            $huella.Add(0.299 * $p.R + 0.587 * $p.G + 0.114 * $p.B)
        }
    }

    $lienzo.Dispose()
    $pequena.Dispose()
    $original.Dispose()
    $memoria.Dispose()

    return $huella
}

$cadena = "Server=7940HS\LOCAL;Database=Catas;Integrated Security=True;TrustServerCertificate=True"
$conexion = New-Object System.Data.SqlClient.SqlConnection $cadena
$conexion.Open()

try
{
    $comando = $conexion.CreateCommand()
    $comando.CommandText = "SELECT Id, Nombre, Foto, Etiqueta FROM Vinos ORDER BY Id"
    $lector = $comando.ExecuteReader()

    $filas = New-Object 'System.Collections.Generic.List[object]'
    while ($lector.Read())
    {
        $filas.Add([pscustomobject]@{
            Id       = $lector.GetInt32(0)
            Nombre   = $lector.GetString(1)
            Foto     = if ($lector.IsDBNull(2)) { $null } else { $lector.GetValue(2) }
            Etiqueta = if ($lector.IsDBNull(3)) { $null } else { $lector.GetValue(3) }
        })
    }
    $lector.Close()
}
finally
{
    $conexion.Close()
}

foreach ($fila in $filas)
{
    $hFoto = ObtenerHuella $fila.Foto
    $hEtiq = ObtenerHuella $fila.Etiqueta

    if ($null -eq $hFoto -or $null -eq $hEtiq)
    {
        Write-Host ("{0,3} {1,-18} falta una de las dos" -f $fila.Id, $fila.Nombre)
        continue
    }

    $suma = 0.0
    for ($i = 0; $i -lt 64; $i++) { $suma += [math]::Abs($hFoto[$i] - $hEtiq[$i]) }
    $distancia = [math]::Round($suma / 64, 1)

    $veredicto = if ($distancia -lt 3) { "IDENTICAS" } elseif ($distancia -lt 12) { "muy parecidas" } else { "distintas" }
    Write-Host ("{0,3} {1,-18} distancia={2,-6} {3}" -f $fila.Id, $fila.Nombre, $distancia, $veredicto)
}
