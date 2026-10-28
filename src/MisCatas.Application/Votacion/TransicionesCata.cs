using MisCatas.Domain.Enums;

namespace MisCatas.Application.Votacion;

/// <summary>
/// Única fuente de verdad de la máquina de estados de una cata:
///
/// <code>
/// Borrador ──abrir──► Abierta ──lanzar 1er voto──► EnCurso ──finalizar──► Finalizada
///                                                     ▲                        │
///                                                     └────────reabrir─────────┘
/// </code>
///
/// Se valida en el servidor, no en la interfaz. Una cata NUNCA se finaliza
/// sola: solo la finaliza el administrador que la controla.
/// </summary>
public static class TransicionesCata
{
    /// <summary>Transiciones permitidas desde cada estado.</summary>
    public static IReadOnlyList<EstadoCata> Destinos(EstadoCata origen) => origen switch
    {
        EstadoCata.Borrador => [EstadoCata.Abierta],
        EstadoCata.Abierta => [EstadoCata.Borrador, EstadoCata.EnCurso, EstadoCata.Finalizada],
        EstadoCata.EnCurso => [EstadoCata.Finalizada],
        EstadoCata.Finalizada => [EstadoCata.EnCurso],
        _ => [],
    };

    /// <summary>Indica si la transición solicitada es legal.</summary>
    public static bool EsValida(EstadoCata origen, EstadoCata destino) =>
        Destinos(origen).Contains(destino);

    /// <summary>
    /// La cata admite cambios de montaje (vinos y participantes) solo mientras
    /// esté en borrador. En cuanto se abre, la mesa queda cerrada.
    /// </summary>
    public static bool AdmiteCambiosDeMontaje(EstadoCata estado) =>
        estado == EstadoCata.Borrador;

    /// <summary>
    /// Los datos del vino solo se destapan cuando la cata está finalizada.
    /// Es la regla de oro de la aplicación.
    /// </summary>
    public static bool DatosRevelados(EstadoCata estado) =>
        estado == EstadoCata.Finalizada;

    /// <summary>Rótulo del botón que ejecuta la transición.</summary>
    public static string TextoTransicion(EstadoCata destino) => destino switch
    {
        EstadoCata.Borrador => "Volver a borrador",
        EstadoCata.Abierta => "Abrir inscripción",
        EstadoCata.EnCurso => "Empezar la cata",
        _ => "Finalizar la cata",
    };
}
