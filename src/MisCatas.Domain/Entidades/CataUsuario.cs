namespace MisCatas.Domain.Entidades;

/// <summary>
/// Invitación de un usuario a una cata (relación N:M).
/// La unicidad de (CataId, UsuarioId) la garantiza un índice único en la base de datos.
/// </summary>
public class CataUsuario
{
    /// <summary>Clave primaria.</summary>
    public int Id { get; set; }

    /// <summary>Cata a la que se invita.</summary>
    public int CataId { get; set; }

    /// <summary>Cata a la que se invita.</summary>
    public Cata Cata { get; set; } = null!;

    /// <summary>Usuario invitado.</summary>
    public int UsuarioId { get; set; }

    /// <summary>Usuario invitado.</summary>
    public Usuario Usuario { get; set; } = null!;
}
