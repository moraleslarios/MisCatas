using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.TiempoReal;

/* ---------------------------------------------------------------------------
   CataHub: el canal en vivo de la cata.

   Reparto de responsabilidades (importante para no romper la sincronía):

     · El SERVIDOR manda. El hub solo difunde el estado que calcula el motor
       (ServicioVotacion + RelojCatas). Aquí NO se decide nada: ni cuándo se
       abre una votación, ni cuándo se cierra por tiempo.

     · El cliente no puede cambiar el estado desde el hub. No hay métodos
       «lanzar», «cerrar» ni «votar»: eso va por los servicios, que validan
       permisos y mando. El hub es de SOLO LECTURA.

     · Al unirse (y al reconectar) el cliente recibe el estado EXACTO del
       momento, con el tiempo restante real. Así recargar el móvil a mitad de
       votación no reinicia nada.

   Grupos: «cata-{id}» para todos y «cata-{id}-{Rol}» por si hace falta
   hablar solo con los escaparates o solo con los administradores.
   --------------------------------------------------------------------------- */

/// <summary>
/// Hub de tiempo real de una cata. Autoalojado en la propia aplicación: con
/// una sola instancia no hace falta backplane ni Azure SignalR Service.
/// </summary>
[Authorize]
public sealed class CataHub(
    IServicioVotacion votacion,
    IServicioParticipacion participacion,
    ILogger<CataHub> log) : Hub
{
    /// <summary>Ruta en la que se publica el hub.</summary>
    public const string Ruta = "/hubs/cata";

    /// <summary>
    /// Suscribe la conexión a los grupos de la cata y devuelve el estado en
    /// vivo del instante, para que el cliente arranque ya sincronizado.
    /// </summary>
    public async Task<EstadoVivoDto?> Unirse(int cataId)
    {
        await ExigirAccesoAsync(cataId);

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            EventosCata.Grupo(cataId),
            Context.ConnectionAborted);

        if (RolActual() is Rol rol)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                EventosCata.Grupo(cataId, rol),
                Context.ConnectionAborted);
        }

        log.LogDebug(
            "Conexión {Conexion} unida a la cata {CataId} como {Rol}.",
            Context.ConnectionId,
            cataId,
            RolActual());

        return await votacion.ObtenerEstadoAsync(cataId, Context.ConnectionAborted);
    }

    /// <summary>Desuscribe la conexión de los grupos de la cata.</summary>
    public async Task Salir(int cataId)
    {
        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            EventosCata.Grupo(cataId),
            Context.ConnectionAborted);

        if (RolActual() is Rol rol)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                EventosCata.Grupo(cataId, rol),
                Context.ConnectionAborted);
        }
    }

    /// <summary>
    /// Estado en vivo actual. Lo piden los clientes al reconectar, para
    /// recuperar el vino en curso y el tiempo restante real.
    /// </summary>
    public async Task<EstadoVivoDto?> PedirEstado(int cataId)
    {
        await ExigirAccesoAsync(cataId);

        return await votacion.ObtenerEstadoAsync(cataId, Context.ConnectionAborted);
    }

    /// <summary>
    /// Métricas agregadas del avance de la cata. Nunca lleva notas ni datos
    /// del vino: solo cuántos han votado, cuántos faltan y el tiempo medio.
    /// </summary>
    public async Task<ProgresoCataDto> PedirResumen(int cataId)
    {
        await ExigirAccesoAsync(cataId);

        return await votacion.ObtenerResumenAsync(cataId, Context.ConnectionAborted);
    }

    /// <summary>
    /// Comprueba que quien llama puede mirar esta cata. Los administradores y
    /// los escaparates ven cualquiera; un usuario, solo las suyas (y así no se
    /// filtra ni la existencia de las demás).
    /// </summary>
    private async Task ExigirAccesoAsync(int cataId)
    {
        if (RolActual() is Rol.Admin or Rol.Escaparate)
        {
            return;
        }

        if (Context.User.ObtenerUsuarioId() is int usuarioId)
        {
            var cata = await participacion.ObtenerCataCiegaAsync(
                cataId,
                usuarioId,
                Context.ConnectionAborted);

            if (cata is not null)
            {
                return;
            }
        }

        log.LogWarning(
            "Acceso rechazado al canal de la cata {CataId} desde la conexión {Conexion}.",
            cataId,
            Context.ConnectionId);

        throw new HubException("No participas en esta cata.");
    }

    /// <summary>Rol del usuario conectado, leído de sus claims.</summary>
    private Rol? RolActual() =>
        Enum.TryParse<Rol>(Context.User?.FindFirst(ClaimTypes.Role)?.Value, out var rol)
            ? rol
            : null;
}
