using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;

namespace MisCatas.Web.TiempoReal;

/* ---------------------------------------------------------------------------
   Notificador real: lleva al navegador lo que decide el motor de la cata.

   Dos ideas que marcan la diferencia con 12 clientes a la vez:

   1) Se manda EL RESULTADO YA CALCULADO en el propio mensaje. Nunca un
      «algo ha cambiado» que provoque que doce terminales consulten la base
      de datos de golpe.

   2) Los resúmenes se AGRUPAN (~500 ms). Cuando diez personas votan casi al
      mismo tiempo no tiene sentido repintar diez veces: se envía el último
      estado conocido una vez por ventana.

   Los ticks del reloj NO se agrupan: son el latido del temporizador y van
   con la cadencia que marca el reloj del servidor.
   --------------------------------------------------------------------------- */

/// <summary>
/// Implementación de <see cref="INotificadorCata"/> sobre SignalR. Sustituye
/// al notificador nulo que registra la capa de infraestructura.
/// </summary>
public sealed class NotificadorCataSignalR(
    IHubContext<CataHub> hub,
    BusCata bus,
    ILogger<NotificadorCataSignalR> log) : INotificadorCata, IDisposable
{
    /// <summary>Ventana de agrupación de los resúmenes.</summary>
    private static readonly TimeSpan Ventana = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Eventos que cambian lo que debe mostrar una pantalla EN ESPERA y que,
    /// por tanto, se replican en la cartelera. Son los que hacen aparecer o
    /// desaparecer la cata del horizonte de quien aguarda; los latidos y los
    /// cambios internos de una votación no pintan nada aquí.
    /// </summary>
    private static readonly HashSet<string> EventosDeCartelera =
    [
        EventosCata.CataAbierta,
        EventosCata.CuentaAtrasIniciada,
        EventosCata.CataFinalizada,
        EventosCata.CataReabierta,
    ];

    private readonly ConcurrentDictionary<int, ResumenPendiente> _pendientes = new();

    /// <inheritdoc />
    public async Task EnviarEstadoAsync(
        int cataId,
        string evento,
        EstadoVivoDto estado,
        CancellationToken ct = default)
    {
        log.LogDebug("Cata {CataId}: enviando «{Evento}».", cataId, evento);

        // Las pantallas de esta aplicación se renderizan en el servidor, así
        // que reciben el aviso por el bus en memoria. El hub se mantiene para
        // cualquier cliente externo. Ambos salen del MISMO objeto de estado:
        // nadie puede ver dos verdades distintas.
        await bus.PublicarEstadoAsync(cataId, evento, estado);

        // Los cambios de «hay o no hay cata» van además por la cartelera, para
        // las pantallas que están en espera y todavía no conocen el id de la
        // cata (inicio del participante y escaparate ocioso).
        if (EventosDeCartelera.Contains(evento))
        {
            await bus.PublicarCarteleraAsync(evento, cataId);
        }

        await hub.Clients
            .Group(EventosCata.Grupo(cataId))
            .SendAsync(evento, estado, ct);
    }

    /// <inheritdoc />
    public async Task EnviarTickAsync(int cataId, EstadoVivoDto estado, CancellationToken ct = default)
    {
        await bus.PublicarTickAsync(cataId, estado);

        await hub.Clients
            .Group(EventosCata.Grupo(cataId))
            .SendAsync(EventosCata.TickTiempoRestante, estado, ct);
    }

    /// <summary>
    /// Envía el resumen agrupando las ráfagas: el primer aviso sale enseguida
    /// y los siguientes se acumulan hasta que se cierra la ventana, momento en
    /// el que se emite el último estado recibido.
    /// </summary>
    public async Task EnviarResumenAsync(
        int cataId,
        ProgresoCataDto progreso,
        CancellationToken ct = default)
    {
        var pendiente = _pendientes.GetOrAdd(cataId, _ => new ResumenPendiente());

        lock (pendiente.Cerrojo)
        {
            pendiente.Ultimo = progreso;

            // Ya hay una ventana abierta: este resumen saldrá al cerrarse.
            if (pendiente.Esperando)
            {
                return;
            }

            pendiente.Esperando = true;
        }

        // La difusión inmediata sí se espera: es barata y así el primer voto
        // de una ráfaga se ve al instante.
        await PublicarAsync(cataId, pendiente);

        // El cierre de la ventana NO se espera. Si lo esperásemos, quien ha
        // votado se quedaría medio segundo mirando el botón.
        _ = CerrarVentanaAsync(cataId, pendiente);
    }

    /// <summary>
    /// Espera la ventana de agrupación y, si durante ella llegaron más votos,
    /// difunde el último estado conocido.
    /// </summary>
    private async Task CerrarVentanaAsync(int cataId, ResumenPendiente pendiente)
    {
        // Sin el CancellationToken de la petición: ese token muere con ella y
        // el resumen rezagado se perdería.
        await Task.Delay(Ventana);

        ProgresoCataDto? rezagado;

        lock (pendiente.Cerrojo)
        {
            rezagado = pendiente.Cambiado ? pendiente.Ultimo : null;
            pendiente.Esperando = false;
        }

        if (rezagado is not null)
        {
            await EnviarResumenAsync(cataId, rezagado);
        }
    }

    private async Task PublicarAsync(int cataId, ResumenPendiente pendiente)
    {
        ProgresoCataDto? progreso;

        lock (pendiente.Cerrojo)
        {
            progreso = pendiente.Ultimo;
            pendiente.Enviado = progreso;
        }

        if (progreso is null)
        {
            return;
        }

        try
        {
            await bus.PublicarResumenAsync(cataId, progreso);

            await hub.Clients
                .Group(EventosCata.Grupo(cataId))
                .SendAsync(EventosCata.ResumenActualizado, progreso);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un fallo al difundir no puede tumbar la votación en curso.
            log.LogWarning(ex, "No se pudo enviar el resumen de la cata {CataId}.", cataId);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _pendientes.Clear();

    /// <summary>Último resumen conocido de una cata y estado de su ventana.</summary>
    private sealed class ResumenPendiente
    {
        public object Cerrojo { get; } = new();

        /// <summary>Resumen más reciente recibido del motor.</summary>
        public ProgresoCataDto? Ultimo { get; set; }

        /// <summary>Último resumen efectivamente difundido.</summary>
        public ProgresoCataDto? Enviado { get; set; }

        /// <summary>Hay una ventana de agrupación abierta.</summary>
        public bool Esperando { get; set; }

        /// <summary>Llegó algo nuevo desde la última difusión.</summary>
        public bool Cambiado => Ultimo is not null && !Equals(Ultimo, Enviado);
    }
}
