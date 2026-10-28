namespace MisCatas.Application.Participacion;

/// <summary>
/// Vista de una cata desde el lado del PARTICIPANTE (y del escaparate).
///
/// Todo lo que devuelve este servicio está ya filtrado en el servidor: los
/// vinos de una cata no finalizada salen como <see cref="VinoCiegoDto"/>,
/// con el número de orden como único dato. Nunca hay que confiar la
/// ocultación a la capa de presentación.
///
/// La fase 3 añadirá aquí los métodos de votación (emitir, rectificar,
/// cerrar). En la fase 2 solo hay consulta.
/// </summary>
public interface IServicioParticipacion
{
    /// <summary>
    /// Catas del usuario repartidas en «En curso», «Próximas» e «Histórico».
    /// </summary>
    Task<MisCatasDto> ObtenerMisCatasAsync(int usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Vista ciega de una cata. Devuelve null si la cata no existe o si el
    /// usuario no es participante (así no se filtra ni su existencia).
    /// </summary>
    Task<CataCiegaDto?> ObtenerCataCiegaAsync(int cataId, int usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Métricas agregadas de avance. Sin notas ni datos de vino.
    /// </summary>
    Task<ProgresoCataDto?> ObtenerProgresoAsync(int cataId, CancellationToken ct = default);

    /// <summary>
    /// Resultado final con todo revelado. Devuelve null si la cata no está
    /// finalizada: es la única puerta por la que se destapan los vinos.
    /// </summary>
    Task<ResultadoCataDto?> ObtenerResultadoAsync(int cataId, int? usuarioId = null, CancellationToken ct = default);

    /// <summary>
    /// Qué debe estar mostrando ahora mismo la pantalla de escaparate.
    /// </summary>
    Task<CataEscaparateDto> ObtenerEscaparateAsync(CancellationToken ct = default);
}
