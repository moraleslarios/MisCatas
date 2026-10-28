namespace MisCatas.Application.Imagenes;

/// <summary>
/// Procesa las imágenes subidas por el administrador: valida el formato,
/// reduce el lado mayor al máximo configurado y recomprime a JPEG.
/// El resultado se guarda en la base de datos como <c>varbinary(max)</c>.
/// </summary>
public interface IServicioImagenes
{
    /// <summary>Extensiones aceptadas al subir una imagen.</summary>
    public static readonly string[] ExtensionesPermitidas =
        [".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif"];

    /// <summary>Tamaño máximo aceptado del archivo subido en bytes (15 MB).</summary>
    public const long TamanoMaximoBytes = 15L * 1024 * 1024;

    /// <summary>
    /// Normaliza una imagen subida.
    /// </summary>
    /// <param name="contenido">Flujo con la imagen original.</param>
    /// <param name="nombreOriginal">Nombre del archivo subido, para validar la extensión.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La imagen redimensionada y recomprimida a JPEG.</returns>
    /// <exception cref="InvalidOperationException">
    /// Si el formato no está admitido o el archivo no es una imagen válida.
    /// </exception>
    Task<ImagenProcesada> ProcesarAsync(
        Stream contenido,
        string nombreOriginal,
        CancellationToken ct = default);

    /// <summary>Indica si la extensión del archivo está admitida.</summary>
    bool EsExtensionPermitida(string nombreArchivo);
}
