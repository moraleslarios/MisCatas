namespace MisCatas.Application.Vinos;

/// <summary>Datos de un vino para listados y detalle.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="Nombre">Nombre comercial.</param>
/// <param name="Anio">Añada.</param>
/// <param name="Clase">Clase o tipo.</param>
/// <param name="Dueno">Quién aporta el vino.</param>
/// <param name="PrecioEstimado">Precio estimado en euros.</param>
/// <param name="Bodega">Bodega.</param>
/// <param name="DenominacionOrigen">Denominación de Origen. Texto libre.</param>
/// <param name="Comentarios">Comentarios libres.</param>
/// <param name="TieneFoto">Si el vino tiene foto de la botella.</param>
/// <param name="TieneEtiqueta">Si el vino tiene foto de la etiqueta.</param>
public record VinoDto(
    int Id,
    string Nombre,
    int? Anio,
    string? Clase,
    string Dueno,
    decimal? PrecioEstimado,
    string? Bodega,
    string? DenominacionOrigen,
    string? Comentarios,
    bool TieneFoto,
    bool TieneEtiqueta)
{
    /// <summary>Nombre con la añada, para mostrar en listados.</summary>
    public string NombreConAnio => Anio.HasValue ? $"{Nombre} ({Anio})" : Nombre;
}

/// <summary>Datos editables de un vino, comunes al alta y a la modificación.</summary>
public class GuardarVinoDto
{
    /// <summary>Identificador. Cero o nulo al crear.</summary>
    public int Id { get; set; }

    /// <summary>Nombre comercial. Obligatorio.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Añada. Opcional.</summary>
    public int? Anio { get; set; }

    /// <summary>Clase o tipo. Opcional.</summary>
    public string? Clase { get; set; }

    /// <summary>Quién aporta el vino. Obligatorio.</summary>
    public string Dueno { get; set; } = string.Empty;

    /// <summary>Precio estimado en euros. Opcional.</summary>
    public decimal? PrecioEstimado { get; set; }

    /// <summary>Bodega. Opcional.</summary>
    public string? Bodega { get; set; }

    /// <summary>Denominación de Origen. Texto libre, opcional.</summary>
    public string? DenominacionOrigen { get; set; }

    /// <summary>Comentarios libres. Opcional.</summary>
    public string? Comentarios { get; set; }
}

/// <summary>Imagen de un vino tal como se sirve al navegador.</summary>
/// <param name="Contenido">Bytes de la imagen.</param>
/// <param name="ContentType">Tipo MIME.</param>
public record ImagenVinoDto(byte[] Contenido, string ContentType);

/// <summary>Distingue las dos imágenes que puede tener un vino.</summary>
public enum TipoImagenVino
{
    /// <summary>Foto general de la botella.</summary>
    Foto = 0,

    /// <summary>Foto de la etiqueta.</summary>
    Etiqueta = 1
}
