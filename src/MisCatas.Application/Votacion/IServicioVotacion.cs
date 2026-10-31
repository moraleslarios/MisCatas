using MisCatas.Application.Comun;
using MisCatas.Application.Participacion;

namespace MisCatas.Application.Votacion;

/// <summary>
/// Motor de la cata en vivo. Concentra TODAS las acciones que cambian el curso
/// de una cata y las valida en el servidor, sin fiarse nunca de la interfaz.
///
/// Reglas duras que esta capa garantiza:
/// <list type="bullet">
///   <item>el reloj lo lleva el servidor; el cliente solo lo pinta;</item>
///   <item>una cata no se finaliza sola: solo la finaliza su administrador;</item>
///   <item>solo gobierna la cata el administrador que la abrió;</item>
///   <item>no se tocan participantes ni vinos con la cata abierta;</item>
///   <item>nada de datos del vino hasta que la cata esté finalizada.</item>
/// </list>
/// </summary>
public interface IServicioVotacion
{
    /// <summary>
    /// Estado en vivo de la cata, reconstruido desde memoria o desde la base de
    /// datos. Es lo que pide un cliente que acaba de recargar o reconectar.
    /// </summary>
    Task<EstadoVivoDto?> ObtenerEstadoAsync(int cataId, CancellationToken ct = default);

    /// <summary>
    /// Abre la cata (Borrador → Abierta) y fija qué administrador la gobierna.
    /// </summary>
    Task<Resultado> AbrirCataAsync(int cataId, int adminId, CancellationToken ct = default);

    /// <summary>
    /// Lanza la votación de un vino por su número de orden. Arranca la cuenta
    /// atrás de preparación (5 → 0) y, al llegar a cero, abre el voto.
    /// La primera llamada pasa la cata de Abierta a EnCurso.
    /// </summary>
    Task<Resultado> LanzarVotacionAsync(int cataId, int numeroOrden, int adminId, CancellationToken ct = default);

    /// <summary>
    /// Cierra a mano la votación en curso aunque quede tiempo. Aplica la
    /// política de no-voto configurada.
    /// </summary>
    Task<Resultado> CerrarVotacionAsync(int cataId, int adminId, CancellationToken ct = default);

    /// <summary>
    /// Congela la votación en marcha: el reloj se detiene y deja de aceptar
    /// votos. Guarda los segundos que quedaban para devolverlos al reanudar.
    /// </summary>
    Task<Resultado> PausarVotacionAsync(int cataId, int adminId, CancellationToken ct = default);

    /// <summary>
    /// Reanuda una votación pausada recalculando el fin previsto con los
    /// segundos que quedaban. Nadie pierde ni gana tiempo.
    /// </summary>
    Task<Resultado> ReanudarVotacionAsync(int cataId, int adminId, CancellationToken ct = default);

    /// <summary>Borra el voto de un usuario en el vino indicado.</summary>
    Task<Resultado> ResetearVotoUsuarioAsync(int cataId, int numeroOrden, int usuarioId, int adminId, CancellationToken ct = default);

    /// <summary>Borra todos los votos del vino indicado y lo deja pendiente.</summary>
    Task<Resultado> ResetearVinoAsync(int cataId, int numeroOrden, int adminId, CancellationToken ct = default);

    /// <summary>Finaliza la cata y destapa los datos de los vinos.</summary>
    Task<Resultado> FinalizarCataAsync(int cataId, int adminId, CancellationToken ct = default);

    /// <summary>Reabre una cata finalizada: los datos se vuelven a ocultar.</summary>
    Task<Resultado> ReabrirCataAsync(int cataId, int adminId, CancellationToken ct = default);

    /// <summary>Lanza la revelación final a pantalla completa.</summary>
    Task<Resultado> ActivarRevelacionAsync(int cataId, int adminId, CancellationToken ct = default);

    /// <summary>Oculta la revelación final antes de que se agote su tiempo.</summary>
    Task<Resultado> OcultarRevelacionAsync(int cataId, int adminId, CancellationToken ct = default);

    /// <summary>
    /// Registra o rectifica el voto de un participante. Hace upsert: el índice
    /// único (CataId, VinoId, UsuarioId) impide duplicados por doble clic.
    /// </summary>
    Task<Resultado<MiVotoDto>> VotarAsync(
        int cataId,
        int numeroOrden,
        int usuarioId,
        decimal nota,
        string? comentario,
        CancellationToken ct = default);

    /// <summary>
    /// Progreso agregado de la cata, ya calculado, para enviarlo tal cual por
    /// SignalR sin que doce clientes consulten la base de datos a la vez.
    /// </summary>
    Task<ProgresoCataDto> ObtenerResumenAsync(int cataId, CancellationToken ct = default);
}

/// <summary>
/// Puente hacia el tiempo real. Vive en la capa de aplicación como abstracción
/// para que el motor no dependa de SignalR; lo implementa la capa web.
/// </summary>
public interface INotificadorCata
{
    /// <summary>Envía un evento de estado a todos los clientes de la cata.</summary>
    Task EnviarEstadoAsync(int cataId, string evento, EstadoVivoDto estado, CancellationToken ct = default);

    /// <summary>Envía el tick del reloj del servidor.</summary>
    Task EnviarTickAsync(int cataId, EstadoVivoDto estado, CancellationToken ct = default);

    /// <summary>Envía el resumen ya calculado (agrupado para evitar ráfagas).</summary>
    Task EnviarResumenAsync(int cataId, ProgresoCataDto progreso, CancellationToken ct = default);

    /// <summary>
    /// Avisa a las pantallas EN ESPERA de que la cartelera ha cambiado: se ha
    /// modificado o borrado una cata y, por tanto, lo que anuncian puede haber
    /// dejado de ser cierto. No transporta datos: cada pantalla vuelve a
    /// preguntar qué le toca mostrar con sus propios permisos.
    /// </summary>
    Task EnviarCarteleraAsync(string evento, int cataId, CancellationToken ct = default);
}
