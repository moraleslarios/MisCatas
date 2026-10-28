namespace MisCatas.Domain.Enums;

/// <summary>
/// Ciclo de vida de una cata.
/// Las transiciones de estado se implementan en la Fase 3; aquí solo se persiste el valor.
/// </summary>
public enum EstadoCata
{
    /// <summary>En preparación: el administrador la está montando.</summary>
    Borrador = 0,

    /// <summary>Publicada: los participantes ya pueden verla, pero todavía no se vota.</summary>
    Abierta = 1,

    /// <summary>Se está votando vino a vino.</summary>
    EnCurso = 2,

    /// <summary>Terminada: resultados visibles y votación cerrada.</summary>
    Finalizada = 3
}
