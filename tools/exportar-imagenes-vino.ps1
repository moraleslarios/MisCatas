# Exporta la Foto y la Etiqueta de un vino a disco para poder inspeccionarlas.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\exportar-imagenes-vino.ps1 -VinoId 14
param(
    [int]$VinoId = 14,
    [string]$Destino = "$env:TEMP\miscatas-imagenes"
)

$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Path $Destino -Force | Out-Null

Add-Type -AssemblyName System.Data

$cadena = "Server=7940HS\LOCAL;Database=Catas;Integrated Security=True;TrustServerCertificate=True"
$conexion = New-Object System.Data.SqlClient.SqlConnection $cadena
$conexion.Open()

try
{
    $comando = $conexion.CreateCommand()
    $comando.CommandText = "SELECT Nombre, Foto, FotoContentType, Etiqueta, EtiquetaContentType FROM Vinos WHERE Id = @id"
    $comando.Parameters.AddWithValue("@id", $VinoId) | Out-Null

    $lector = $comando.ExecuteReader()

    if (-not $lector.Read())
    {
        Write-Host "No existe el vino $VinoId"
        return
    }

    $nombre = $lector.GetString(0)
    Write-Host "Vino $VinoId : $nombre"

    if (-not $lector.IsDBNull(1))
    {
        $bytes = $lector.GetValue(1)
        $ruta = Join-Path $Destino "vino-$VinoId-foto.jpg"
        [System.IO.File]::WriteAllBytes($ruta, $bytes)
        Write-Host "  Foto      -> $ruta ($($bytes.Length) bytes, $($lector.GetString(2)))"
    }

    if (-not $lector.IsDBNull(3))
    {
        $bytes = $lector.GetValue(3)
        $ruta = Join-Path $Destino "vino-$VinoId-etiqueta.jpg"
        [System.IO.File]::WriteAllBytes($ruta, $bytes)
        Write-Host "  Etiqueta  -> $ruta ($($bytes.Length) bytes, $($lector.GetString(4)))"
    }

    $lector.Close()
}
finally
{
    $conexion.Close()
}
