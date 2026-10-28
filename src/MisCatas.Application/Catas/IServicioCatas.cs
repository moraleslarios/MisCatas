using MisCatas.Application.Comun;
using MisCatas.Domain.Enums;

namespace MisCatas.Application.Catas;

/// <summary>
/// Administración de catas: alta, edición, asignación de vinos y participantes.
/// El motor de votación en directo se construye en la Fase 3.
/// </summary>
public interface IServicioCatas
{
    /// <summary>Obtiene una página de catas, opcionalmente filtrada.</summary>
    /// <param name="busqueda">Texto a buscar en nombre o lugar.</param>
    /// <param name="estado">Estado por el que filtrar, o nulo para todos.</param>
    /// <param name="pagina">Número de página (empieza en 1).</param>
    /// <param name="tamanoPagina">Elementos por página.</param>
    /// <param name="ct">Token de cancelación.</param>
    Task<ResultadoPaginado<CataDto>> ListarAsync(
        string? busqueda = null,
        EstadoCata? estado = null,
        int pagina = 1,
        int tamanoPagina = 20,
        CancellationToken ct = default);

    /// <summary>Obtiene una cata con sus vinos y participantes.</summary>
    Task<CataDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct = default);

    /// <summary>Crea una cata en estado Borrador y devuelve su identificador.</summary>
    Task<Resultado<int>> CrearAsync(GuardarCataDto datos, CancellationToken ct = default);

    /// <summary>Modifica los datos generales de una cata.</summary>
    Task<Resultado> EditarAsync(GuardarCataDto datos, CancellationToken ct = default);

    /// <summary>
    /// Elimina una cata. Falla si ya tiene votos emitidos.
    /// </summary>
    Task<Resultado> EliminarAsync(int id, CancellationToken ct = default);

    // --- Vinos de la cata ---

    /// <summary>
    /// Añade un vino al final del recorrido de la cata.
    /// Falla si el vino ya está asignado o si la cata no es editable.
    /// </summary>
    Task<Resultado> AgregarVinoAsync(int cataId, int vinoId, CancellationToken ct = default);

    /// <summary>Quita un vino de la cata y recompacta los números de orden.</summary>
    Task<Resultado> QuitarVinoAsync(int cataId, int vinoId, CancellationToken ct = default);

    /// <summary>
    /// Reordena los vinos de la cata según la secuencia de identificadores
    /// de vino recibida, asignando órdenes consecutivos desde 1.
    /// </summary>
    Task<Resultado> ReordenarVinosAsync(
        int cataId,
        IReadOnlyList<int> vinoIdsEnOrden,
        CancellationToken ct = default);

    /// <summary>
    /// Baraja al azar el recorrido de los vinos de la cata.
    /// Solo se permite si la cata es editable y ningún vino se ha votado
    /// todavía, para no desbaratar una cata ya en marcha.
    /// </summary>
    Task<Resultado> OrdenarVinosAlAzarAsync(int cataId, CancellationToken ct = default);

    // --- Participantes de la cata ---

    /// <summary>Invita un usuario a la cata. Falla si ya estaba invitado.</summary>
    Task<Resultado> AgregarParticipanteAsync(
        int cataId,
        int usuarioId,
        CancellationToken ct = default);

    /// <summary>Retira la invitación de un usuario. Falla si ya ha votado.</summary>
    Task<Resultado> QuitarParticipanteAsync(
        int cataId,
        int usuarioId,
        CancellationToken ct = default);

    /// <summary>
    /// Sustituye la lista de participantes de la cata por la indicada.
    /// Falla si se intenta retirar a alguien que ya ha votado.
    /// </summary>
    Task<Resultado> EstablecerParticipantesAsync(
        int cataId,
        IReadOnlyList<int> usuarioIds,
        CancellationToken ct = default);

    // --- Ciclo de vida ---

    /// <summary>
    /// Devuelve la única cata que puede estar activa (Abierta o EnCurso), o
    /// nulo si no hay ninguna. Es la consulta que usan las pantallas de inicio
    /// para avisar al usuario de que tiene una cata a medias.
    /// </summary>
    Task<CataDto?> ObtenerCataActivaAsync(CancellationToken ct = default);

    /// <summary>
    /// Cambia el estado de la cata validando la transición y los requisitos
    /// (por ejemplo, no se puede abrir una cata sin vinos ni participantes).
    /// Rechaza activar una cata si ya hay otra abierta o en curso.
    /// </summary>
    Task<Resultado> CambiarEstadoAsync(
        int cataId,
        EstadoCata nuevoEstado,
        CancellationToken ct = default);
}
