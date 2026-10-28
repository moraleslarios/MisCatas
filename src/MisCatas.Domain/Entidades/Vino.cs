namespace MisCatas.Domain.Entidades;

/// <summary>
/// Un vino del catálogo. Puede participar en varias catas.
/// Las imágenes se guardan en la base de datos como <c>varbinary(max)</c>, ya
/// redimensionadas en el servidor (máx. 1200 px) y recomprimidas a JPEG.
/// </summary>
public class Vino
{
    /// <summary>Clave primaria.</summary>
    public int Id { get; set; }

    /// <summary>Nombre comercial del vino. Obligatorio.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Añada. Opcional.</summary>
    public int? Anio { get; set; }

    /// <summary>Clase o tipo (crianza, reserva, joven...). Texto libre, opcional.</summary>
    public string? Clase { get; set; }

    /// <summary>
    /// Quién aporta el vino a la cata. Obligatorio: es el ganador de la cata
    /// si su vino obtiene la mejor nota. Texto libre.
    /// </summary>
    public string Dueno { get; set; } = string.Empty;

    /// <summary>Precio estimado en euros. Opcional.</summary>
    public decimal? PrecioEstimado { get; set; }

    /// <summary>Bodega. Texto libre, opcional.</summary>
    public string? Bodega { get; set; }

    /// <summary>
    /// Denominación de Origen (Rioja, Ribera del Duero, Rueda...). Texto
    /// libre y opcional: hay vinos de mesa y vinos de autor sin D.O.
    /// </summary>
    public string? DenominacionOrigen { get; set; }

    /// <summary>Comentarios libres de longitud arbitraria.</summary>
    public string? Comentarios { get; set; }

    /// <summary>Foto general de la botella. Se sirve por endpoint, nunca embebida en el HTML.</summary>
    public byte[]? Foto { get; set; }

    /// <summary>Tipo MIME de <see cref="Foto"/> (normalmente <c>image/jpeg</c>).</summary>
    public string? FotoContentType { get; set; }

    /// <summary>Foto de la etiqueta. Se sirve por endpoint, nunca embebida en el HTML.</summary>
    public byte[]? Etiqueta { get; set; }

    /// <summary>Tipo MIME de <see cref="Etiqueta"/> (normalmente <c>image/jpeg</c>).</summary>
    public string? EtiquetaContentType { get; set; }

    // --- Propiedades de navegación ---

    /// <summary>Catas en las que participa este vino.</summary>
    public ICollection<CataVino> Participaciones { get; set; } = new List<CataVino>();

    /// <summary>Votos recibidos por este vino.</summary>
    public ICollection<Voto> Votos { get; set; } = new List<Voto>();
}
