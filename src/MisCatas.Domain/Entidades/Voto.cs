namespace MisCatas.Domain.Entidades;

/// <summary>
/// Puntuación de un usuario a un vino en una cata concreta.
/// Solo puede existir un voto por combinación (cata, vino, usuario): lo garantiza
/// un índice único en la base de datos, no la interfaz.
/// </summary>
public class Voto
{
    /// <summary>Clave primaria.</summary>
    public int Id { get; set; }

    /// <summary>Cata en la que se emite el voto.</summary>
    public int CataId { get; set; }

    /// <summary>Cata en la que se emite el voto.</summary>
    public Cata Cata { get; set; } = null!;

    /// <summary>Vino puntuado.</summary>
    public int VinoId { get; set; }

    /// <summary>Vino puntuado.</summary>
    public Vino Vino { get; set; } = null!;

    /// <summary>Usuario que vota.</summary>
    public int UsuarioId { get; set; }

    /// <summary>Usuario que vota.</summary>
    public Usuario Usuario { get; set; } = null!;

    /// <summary>
    /// Nota entre 0,0 y 10,0 con un decimal.
    /// Es nullable para poder registrar un no-voto cuando la política
    /// configurada es <c>NoCuenta</c>.
    /// </summary>
    public decimal? Nota { get; set; }

    /// <summary>Comentario libre del votante.</summary>
    public string? Comentario { get; set; }

    /// <summary>Segundos que tardó el usuario en votar desde que se abrió el turno.</summary>
    public int SegundosEnVotar { get; set; }

    /// <summary>Momento (UTC) del voto.</summary>
    public DateTime FechaVotoUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Indica si el usuario modificó su voto después de emitirlo.</summary>
    public bool FueRectificado { get; set; }

    /// <summary>
    /// Indica que el voto lo generó el sistema porque el usuario no votó a tiempo.
    /// </summary>
    public bool EsAutomatico { get; set; }

    /// <summary>
    /// Token de concurrencia optimista: evita que dos envíos simultáneos
    /// del mismo voto se solapen.
    /// </summary>
    public byte[]? RowVersion { get; set; }
}
