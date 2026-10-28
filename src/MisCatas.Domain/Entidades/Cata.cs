using MisCatas.Domain.Enums;

namespace MisCatas.Domain.Entidades;

/// <summary>
/// Un evento de cata: un grupo de participantes puntúa una lista ordenada de vinos.
/// En la Fase 1 solo se modela y se administra; el motor de votación en
/// tiempo real llega en la Fase 3.
/// </summary>
public class Cata
{
    /// <summary>Clave primaria.</summary>
    public int Id { get; set; }

    /// <summary>Nombre del evento. Obligatorio.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Dónde se celebra. Opcional.</summary>
    public string? Lugar { get; set; }

    /// <summary>Fecha prevista de celebración.</summary>
    public DateTime Fecha { get; set; } = DateTime.Today;

    /// <summary>Notas libres sobre la cata.</summary>
    public string? Comentarios { get; set; }

    /// <summary>Estado del ciclo de vida.</summary>
    public EstadoCata Estado { get; set; } = EstadoCata.Borrador;

    /// <summary>Momento (UTC) en que la cata pasó a <see cref="EstadoCata.Abierta"/>.</summary>
    public DateTime? FechaApertura { get; set; }

    /// <summary>Momento (UTC) en que la cata pasó a <see cref="EstadoCata.Finalizada"/>.</summary>
    public DateTime? FechaFinalizacion { get; set; }

    /// <summary>Vino cuyo turno está activo. Nulo si no se está votando.</summary>
    public int? VinoActualId { get; set; }

    /// <summary>Vino cuyo turno está activo.</summary>
    public Vino? VinoActual { get; set; }

    /// <summary>Administrador que lleva el mando de la cata en directo. Opcional.</summary>
    public int? AdminControlaId { get; set; }

    /// <summary>Administrador que lleva el mando de la cata en directo.</summary>
    public Usuario? AdminControla { get; set; }

    /// <summary>
    /// Segundos para votar cada vino en esta cata.
    /// Si es nulo se usa el valor global de <see cref="Configuracion.TiempoVotoSegundos"/>.
    /// </summary>
    public int? TiempoVotoSegundos { get; set; }

    /// <summary>Indica si se está mostrando la revelación final de resultados.</summary>
    public bool RevelacionActiva { get; set; }

    /// <summary>
    /// Token de concurrencia optimista: evita que dos administradores pisen
    /// el estado de la misma cata simultáneamente.
    /// </summary>
    public byte[]? RowVersion { get; set; }

    // --- Propiedades de navegación ---

    /// <summary>Participantes invitados.</summary>
    public ICollection<CataUsuario> Participantes { get; set; } = new List<CataUsuario>();

    /// <summary>Vinos a catar, con su orden.</summary>
    public ICollection<CataVino> Vinos { get; set; } = new List<CataVino>();

    /// <summary>Votos emitidos en esta cata.</summary>
    public ICollection<Voto> Votos { get; set; } = new List<Voto>();
}
