using System.Collections.Concurrent;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;

namespace MisCatas.Web.TiempoReal;

/* ---------------------------------------------------------------------------
   BusCata: el reparto interno de los avisos del motor.

   ¿Por qué un bus y no un cliente de SignalR dentro de Blazor?

   Esta aplicación se renderiza con InteractiveServer: los componentes NO se
   ejecutan en el móvil, se ejecutan en el servidor y el navegador solo recibe
   el repintado por el WebSocket del circuito de Blazor. Si un componente
   abriese además una conexión de SignalR contra su propio servidor tendríamos:

     · Dos WebSockets por persona (el del circuito y el del hub) y, con doce
       móviles y dos televisores, veintiocho conexiones para mover los mismos
       datos.
     · El problema de la cookie: el cliente de SignalR viviría en el servidor,
       donde ya no hay HttpContext del que copiar la autenticación.
     · Un salto de red (y de serialización) para entregar un objeto que ya
       está en memoria, en el mismo proceso.

   Así que los avisos se reparten en memoria, que es lo natural aquí, y el hub
   sigue publicado para cualquier cliente de verdad externo. Una sola fuente:
   el notificador alimenta el hub Y este bus, de modo que nadie pueda ver dos
   verdades distintas.

   El bus NO calcula nada ni guarda estado de la cata: solo entrega lo que el
   motor ya decidió. El servidor sigue siendo el único dueño del tiempo.
   --------------------------------------------------------------------------- */

/// <summary>
/// Reparto en memoria de los avisos en vivo de las catas. Registrado como
/// singleton: vive mientras viva la aplicación y es independiente de los
/// circuitos de Blazor que se conecten y se desconecten.
/// </summary>
public sealed class BusCata
{
    private readonly ConcurrentDictionary<int, Canal> _canales = new();

    /// <summary>
    /// Oyentes de la CARTELERA: avisos que no van dirigidos a una cata
    /// concreta, sino a quien está esperando a que «pase algo».
    ///
    /// Hace falta un canal aparte porque quien aguarda en la pantalla de
    /// inicio, o la televisión en espera, todavía NO conoce el identificador
    /// de la cata que el administrador va a abrir, así que no puede
    /// suscribirse a ella. El valor del diccionario no se usa: hace de
    /// conjunto concurrente.
    /// </summary>
    private readonly ConcurrentDictionary<IOyenteCartelera, byte> _cartelera = new();

    private readonly ILogger<BusCata> _log;

    public BusCata(ILogger<BusCata> log) => _log = log;

    /// <summary>
    /// Suscribe un oyente a los avisos de una cata. Hay que liberar el
    /// <see cref="IDisposable"/> devuelto al abandonar la pantalla: si no, el
    /// circuito seguiría recibiendo avisos después de cerrarse.
    /// </summary>
    public IDisposable Suscribir(int cataId, IOyenteCata oyente)
    {
        ArgumentNullException.ThrowIfNull(oyente);

        var canal = _canales.GetOrAdd(cataId, _ => new Canal());
        canal.Oyentes[oyente] = 0;

        _log.LogDebug(
            "Cata {CataId}: nuevo oyente en el bus ({Total} en total).",
            cataId,
            canal.Oyentes.Count);

        return new Suscripcion(this, cataId, oyente);
    }

    /// <summary>Reparte un cambio de estado (apertura, cierre, revelación…).</summary>
    public Task PublicarEstadoAsync(int cataId, string evento, EstadoVivoDto estado) =>
        RepartirAsync(cataId, oyente => oyente.RecibirEstadoAsync(evento, estado));

    /// <summary>Reparte un latido del reloj del servidor.</summary>
    public Task PublicarTickAsync(int cataId, EstadoVivoDto estado) =>
        RepartirAsync(cataId, oyente => oyente.RecibirTickAsync(estado));

    /// <summary>Reparte el avance de la votación (ya agrupado por el notificador).</summary>
    public Task PublicarResumenAsync(int cataId, ProgresoCataDto progreso) =>
        RepartirAsync(cataId, oyente => oyente.RecibirResumenAsync(progreso));

    // ----------------------------------------------------------- cartelera

    /// <summary>
    /// Suscribe un oyente a la cartelera. Hay que liberar el
    /// <see cref="IDisposable"/> devuelto al abandonar la pantalla.
    /// </summary>
    public IDisposable SuscribirCartelera(IOyenteCartelera oyente)
    {
        ArgumentNullException.ThrowIfNull(oyente);

        _cartelera[oyente] = 0;

        _log.LogDebug(
            "Cartelera: nuevo oyente ({Total} en total).",
            _cartelera.Count);

        return new SuscripcionCartelera(this, oyente);
    }

    /// <summary>
    /// Avisa a TODAS las pantallas en espera de que la cata activa ha
    /// cambiado (se ha abierto, ha arrancado o ha terminado), para que
    /// vuelvan a preguntar qué les toca mostrar.
    ///
    /// Solo viaja el identificador y el nombre del evento: cada pantalla
    /// relee sus propios datos con SUS permisos, de modo que la cartelera no
    /// puede filtrar información de una cata a quien no está invitado.
    /// </summary>
    public async Task PublicarCarteleraAsync(string evento, int cataId)
    {
        if (_cartelera.IsEmpty)
        {
            return;
        }

        foreach (var oyente in _cartelera.Keys)
        {
            try
            {
                await oyente.RecibirCarteleraAsync(evento, cataId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogDebug(
                    ex,
                    "Cartelera: un oyente no pudo atender el aviso «{Evento}»; se le retira.",
                    evento);

                _cartelera.TryRemove(oyente, out _);
            }
        }
    }

    private async Task RepartirAsync(int cataId, Func<IOyenteCata, Task> entrega)
    {
        if (!_canales.TryGetValue(cataId, out var canal) || canal.Oyentes.IsEmpty)
        {
            return;
        }

        foreach (var oyente in canal.Oyentes.Keys)
        {
            try
            {
                await entrega(oyente);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogDebug(
                    ex,
                    "Cata {CataId}: un oyente no pudo atender el aviso; se le retira del bus.",
                    cataId);

                canal.Oyentes.TryRemove(oyente, out _);
            }
        }
    }

    private void Cancelar(int cataId, IOyenteCata oyente)
    {
        if (!_canales.TryGetValue(cataId, out var canal))
        {
            return;
        }

        canal.Oyentes.TryRemove(oyente, out _);

        // Sin oyentes no merece la pena conservar el canal: la próxima
        // suscripción lo vuelve a crear.
        if (canal.Oyentes.IsEmpty)
        {
            _canales.TryRemove(cataId, out _);
        }
    }

    /// <summary>Oyentes de una cata. El valor del diccionario no se usa: hace de conjunto concurrente.</summary>
    private sealed class Canal
    {
        public ConcurrentDictionary<IOyenteCata, byte> Oyentes { get; } = new();
    }

    private sealed class Suscripcion(BusCata bus, int cataId, IOyenteCata oyente) : IDisposable
    {
        private bool _liberada;

        public void Dispose()
        {
            if (_liberada)
            {
                return;
            }

            _liberada = true;
            bus.Cancelar(cataId, oyente);
        }
    }

    private sealed class SuscripcionCartelera(BusCata bus, IOyenteCartelera oyente) : IDisposable
    {
        private bool _liberada;

        public void Dispose()
        {
            if (_liberada)
            {
                return;
            }

            _liberada = true;
            bus._cartelera.TryRemove(oyente, out _);
        }
    }
}

/// <summary>
/// Quien quiera enterarse de lo que pasa en una cata. Lo implementa
/// <see cref="ClienteCataHub"/>; los componentes hablan con el cliente, no
/// con el bus.
/// </summary>
public interface IOyenteCata
{
    /// <summary>Cambio de estado con nombre de evento (ver <see cref="EventosCata"/>).</summary>
    Task RecibirEstadoAsync(string evento, EstadoVivoDto estado);

    /// <summary>Latido del reloj del servidor.</summary>
    Task RecibirTickAsync(EstadoVivoDto estado);

    /// <summary>Avance de la votación en curso.</summary>
    Task RecibirResumenAsync(ProgresoCataDto progreso);
}

/// <summary>
/// Quien está esperando a que «pase algo» sin saber todavía en qué cata:
/// la pantalla de inicio del participante y el escaparate en espera.
///
/// Es un canal deliberadamente pobre: solo dice QUÉ ha pasado y en qué cata,
/// nunca datos de la cata. Quien lo recibe vuelve a consultar sus propios
/// datos con sus propios permisos.
/// </summary>
public interface IOyenteCartelera
{
    /// <summary>
    /// La cata activa del sistema ha cambiado de situación. El evento es uno
    /// de <see cref="EventosCata"/> (típicamente <c>CataAbierta</c>,
    /// <c>CuentaAtrasIniciada</c>, <c>CataFinalizada</c> o <c>CataReabierta</c>).
    /// </summary>
    Task RecibirCarteleraAsync(string evento, int cataId);
}
