# Informa de las dimensiones de la Foto y la Etiqueta de todos los vinos.
# Sirve para saber si lo subido en cada hueco es coherente (una etiqueta suele
# ser casi cuadrada u horizontal; una foto de botella es claramente vertical).
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\medir-imagenes-vinos.ps1

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Data
Add-Type -AssemblyName System.Drawing

$cadena = "Server=7940HS\LOCAL;Database=Catas;Integrated Security=True;TrustServerCertificate=True"
$conexion = New-Object System.Data.SqlClient.SqlConnection $cadena
$conexion.Open()

function Medir([byte[]]$bytes)
{
    if ($null -eq $bytes) { return "sin imagen" }

    $memoria = New-Object System.IO.MemoryStream(, $bytes)
    try
    {
        $imagen = [System.Drawing.Image]::FromStream($memoria)
        try
        {
            $relacion = [math]::Round($imagen.Width / $imagen.Height, 2)
            $forma = if ($relacion -lt 0.8) { "VERTICAL" } elseif ($relacion -gt 1.25) { "horizontal" } else { "cuadrada" }
            return "$($imagen.Width)x$($imagen.Height) ratio=$relacion $forma"
        }
        finally { $imagen.Dispose() }
    }
    finally { $memoria.Dispose() }
}

try
{
    $comando = $conexion.CreateCommand()
    $comando.CommandText = "SELECT Id, Nombre, Foto, Etiqueta FROM Vinos ORDER BY Id"
    $lector = $comando.ExecuteReader()

    while ($lector.Read())
    {
        $id = $lector.GetInt32(0)
        $nombre = $lector.GetString(1)
        $foto = if ($lector.IsDBNull(2)) { $null } else { $lector.GetValue(2) }
        $etiqueta = if ($lector.IsDBNull(3)) { $null } else { $lector.GetValue(3) }

        Write-Host ("{0,3} {1,-18} Foto: {2,-34} Etiqueta: {3}" -f $id, $nombre, (Medir $foto), (Medir $etiqueta))
    }

    $lector.Close()
}
finally
{
    $conexion.Close()
}
