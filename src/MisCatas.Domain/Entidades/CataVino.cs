using MisCatas.Domain.Enums;

namespace MisCatas.Domain.Entidades;

/// <summary>
/// Un vino incluido en una cata, con su posición en el recorrido (relación N:M con datos).
/// La base de datos garantiza con índices únicos que no se repita el vino
/// ni el número de orden dentro de la misma cata.
/// </summary>
public class CataVino
{
    /// <summary>Clave primaria.</summary>
    public int Id { get; set; }

    /// <summary>Cata a la que pertenece.</summary>
    public int CataId { get; set; }

    /// <summary>Cata a la que pertenece.</summary>
    public Cata Cata { get; set; } = null!;

    /// <summary>Vino incluido.</summary>
    public int VinoId { get; set; }

    /// <summary>Vino incluido.</summary>
    public Vino Vino { get; set; } = null!;

    /// <summary>
    /// Número de orden visible para el participante (vino 1, vino 2...).
    /// Único dentro de la cata.
    /// </summary>
    public int Orden { get; set; }

    /// <summary>Estado de la votación de este vino.</summary>
    public EstadoVotacion EstadoVotacion { get; set; } = EstadoVotacion.Pendiente;

    /// <summary>Momento (UTC) en que se abrió la votación de este vino.</summary>
    public DateTime? InicioVotacionUtc { get; set; }

    /// <summary>Momento (UTC) en que se cierra automáticamente la votación.</summary>
    public DateTime? FinPrevistoUtc { get; set; }

    /// <summary>
    /// Segundos que quedaban cuando el administrador PAUSÓ la votación.
    ///
    /// Es el único testigo persistente de la pausa: mientras no sea nulo y la
    /// votación siga <see cref="EstadoVotacion.Abierta"/>, el reloj está
    /// congelado y no se aceptan votos. Al reanudar se vuelve a nulo y se
    /// recalcula <see cref="FinPrevistoUtc"/>, de modo que un reinicio del
    /// proceso en mitad de la pausa no regala ni roba tiempo a nadie.
    /// </summary>
    public int? SegundosPausaRestantes { get; set; }
}
