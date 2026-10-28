namespace MisCatas.Domain.Enums;

/// <summary>
/// Estado de la votación de un vino concreto dentro de una cata.
/// </summary>
public enum EstadoVotacion
{
    /// <summary>Aún no ha llegado su turno.</summary>
    Pendiente = 0,

    /// <summary>Turno activo: se aceptan votos.</summary>
    Abierta = 1,

    /// <summary>Turno terminado: ya no se aceptan votos.</summary>
    Cerrada = 2
}
