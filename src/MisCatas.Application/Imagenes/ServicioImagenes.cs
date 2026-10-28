using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace MisCatas.Application.Imagenes;

/// <summary>
/// Implementación de <see cref="IServicioImagenes"/> con ImageSharp.
/// Devuelve los bytes del JPEG para almacenarlos en la base de datos.
/// </summary>
public class ServicioImagenes : IServicioImagenes
{
    /// <summary>Tipo MIME de todas las imágenes que genera este servicio.</summary>
    public const string ContentTypeJpeg = "image/jpeg";

    private readonly OpcionesImagenes _opciones;

    public ServicioImagenes(IOptions<OpcionesImagenes> opciones)
    {
        _opciones = opciones.Value;
    }

    public bool EsExtensionPermitida(string nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo))
        {
            return false;
        }

        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        return IServicioImagenes.ExtensionesPermitidas.Contains(extension);
    }

    public async Task<ImagenProcesada> ProcesarAsync(
        Stream contenido,
        string nombreOriginal,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contenido);

        if (!EsExtensionPermitida(nombreOriginal))
        {
            throw new InvalidOperationException(
                $"Formato de imagen no admitido. Admitidos: " +
                $"{string.Join(", ", IServicioImagenes.ExtensionesPermitidas)}.");
        }

        Image imagen;
        try
        {
            // ImageSharp detecta el formato de entrada automáticamente.
            imagen = await Image.LoadAsync(contenido, ct);
        }
        catch (UnknownImageFormatException)
        {
            throw new InvalidOperationException("El archivo no es una imagen válida.");
        }
        catch (InvalidImageContentException)
        {
            throw new InvalidOperationException("La imagen está dañada o incompleta.");
        }

        using (imagen)
        {
            // Respeta la orientación EXIF de las fotos hechas con el móvil.
            imagen.Mutate(x => x.AutoOrient());

            // Solo reducimos; nunca ampliamos una imagen pequeña.
            var ladoMayor = Math.Max(imagen.Width, imagen.Height);
            if (ladoMayor > _opciones.LadoMaximoPx)
            {
                imagen.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(_opciones.LadoMaximoPx, _opciones.LadoMaximoPx)
                }));
            }

            var codificador = new JpegEncoder { Quality = _opciones.CalidadJpeg };

            using var memoria = new MemoryStream();
            await imagen.SaveAsync(memoria, codificador, ct);

            return new ImagenProcesada(
                memoria.ToArray(),
                ContentTypeJpeg,
                imagen.Width,
                imagen.Height);
        }
    }
}
