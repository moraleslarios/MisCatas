namespace MisCatas.Application.Imagenes;

/// <summary>Opciones de procesado de imágenes.</summary>
public class OpcionesImagenes
{
    /// <summary>Clave de la sección en appsettings.json.</summary>
    public const string Seccion = "Imagenes";

    /// <summary>Lado mayor máximo en píxeles. Las imágenes mayores se reducen.</summary>
    public int LadoMaximoPx { get; set; } = 1200;

    /// <summary>Calidad JPEG de salida (1-100).</summary>
    public int CalidadJpeg { get; set; } = 82;
}
