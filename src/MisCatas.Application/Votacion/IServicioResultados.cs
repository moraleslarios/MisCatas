using MisCatas.Application.Participacion;

namespace MisCatas.Application.Votacion;

/// <summary>
/// Servicio de resultados de una cata. Es el ÚNICO sitio del sistema que
/// decide quién gana, para que la clasificación sea exactamente la misma en la
/// pantalla del participante, en el panel del administrador y en el
/// escaparate.
///
/// Reglas del concurso que garantiza:
/// <list type="bullet">
///   <item>la nota de un vino es la MEDIA ARITMÉTICA de sus notas válidas;</item>
///   <item>los no-votos con política <c>NoCuenta</c> se excluyen de la media;</item>
///   <item>en caso de empate gana el de MENOR DESVIACIÓN TÍPICA (más consenso);</item>
///   <item>nada se destapa si la cata no está finalizada.</item>
/// </list>
///
/// El cálculo puro vive en <see cref="CalculadoraResultados"/>; este servicio
/// solo se encarga de leer la base de datos y de vestir el resultado.
/// </summary>
public interface IServicioResultados
{
    /// <summary>
    /// Clasificación completa de una cata FINALIZADA, con todos los datos de
    /// los vinos al descubierto. Devuelve <c>null</c> si la cata no existe o
    /// si todavía no ha terminado: esa es la puerta de la cata ciega.
    /// </summary>
    /// <param name="cataId">Cata de la que se quiere el resultado.</param>
    /// <param name="usuarioId">
    /// Participante que mira, para poder marcar su propio voto en cada vino.
    /// </param>
    Task<ResultadoCataDto?> ObtenerResultadoAsync(
        int cataId,
        int? usuarioId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Métricas y orden de los vinos SIN destapar su identidad. Vale para el
    /// panel del administrador y para el escaparate mientras la cata sigue
    /// viva: solo devuelve números de orden, medias y consenso.
    /// </summary>
    Task<IReadOnlyList<MetricasVino>> ObtenerMetricasAsync(
        int cataId,
        CancellationToken ct = default);

    /// <summary>
    /// Métricas de un único vino por su número de orden, para refrescar el
    /// resumen en vivo justo al cerrar su votación.
    /// </summary>
    Task<MetricasVino?> ObtenerMetricasVinoAsync(
        int cataId,
        int numeroOrden,
        CancellationToken ct = default);
}
