using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;

namespace MisCatas.Web.TiempoReal;

/* ---------------------------------------------------------------------------
   ClienteCataHub: lo que usan las pantallas para estar en vivo.

   Reglas que respeta, porque son las que mantienen a doce móviles y dos
   televisores enseñando lo mismo:

   1) NO HAY TEMPORIZADOR PROPIO. El tiempo que se pinta sale siempre del
      último estado que mandó el servidor. Entre dos latidos se INTERPOLA con
      el reloj local (para que los segundos no se vean a saltos), pero en
      cuanto llega un latido nuevo se descarta la interpolación. Así un móvil
      con el reloj desajustado, o que estuvo con la pantalla apagada, no se
      desincroniza: se corrige en el siguiente latido.

   2) El estado inicial se PIDE al conectar. Recargar la página recupera el
      vino en curso, la fase y el tiempo restante real.

   3) Los eventos llegan con el dato ya calculado. El cliente no consulta la
      base de datos al recibir un aviso.

   Uso típico en un componente:

       @implements IAsyncDisposable
       @inject ClienteCataHub Vivo

       protected override async Task OnInitializedAsync()
       {
           Vivo.Cambio += AlCambiar;
           await Vivo.ConectarAsync(CataId);
       }

       private async Task AlCambiar() => await InvokeAsync(StateHasChanged);

       public async ValueTask DisposeAsync()
       {
           Vivo.Cambio -= AlCambiar;
           await Vivo.DisposeAsync();
       }

   Es «Scoped»: una instancia por circuito de Blazor, es decir, una por
   pestaña abierta.
   --------------------------------------------------------------------------- */

/// <summary>
/// Conexión en vivo de una pantalla con una cata. Guarda el último estado
/// conocido y avisa al componente cuando algo cambia.
/// </summary>
public sealed class ClienteCataHub : IOyenteCata, IAsyncDisposable
{
    private readonly BusCata _bus;
    private readonly IServicioVotacion _votacion;
    private readonly ILogger<ClienteCataHub> _log;

    private IDisposable? _suscripcion;
    private DateTime _recibidoUtc;
    private bool _liberado;

    public ClienteCataHub(
        BusCata bus,
        IServicioVotacion votacion,
        ILogger<ClienteCataHub> log)
    {
        _bus = bus;
        _votacion = votacion;
        _log = log;
    }

    /// <summary>
    /// Salta en cada aviso recibido. El componente debe responder con
    /// <c>InvokeAsync(StateHasChanged)</c>, nunca con un <c>StateHasChanged</c>
    /// a secas: el aviso llega desde un hilo que no es el del renderizador.
    /// </summary>
    public event Func<Task>? Cambio;

    /// <summary>Identificador de la cata a la que se está escuchando.</summary>
    public int? CataId { get; private set; }

    /// <summary>Último estado en vivo que mandó el servidor.</summary>
    public EstadoVivoDto? Estado { get; private set; }

    /// <summary>Último avance de la votación que mandó el servidor.</summary>
    public ProgresoCataDto? Progreso { get; private set; }

    /// <summary>Nombre del último evento recibido, por si la pantalla quiere reaccionar.</summary>
    public string? UltimoEvento { get; private set; }

    /// <summary>
    /// Hay conexión con el canal de la cata. Cuando es <c>false</c> la
    /// pantalla debe mostrar su propio aviso de «Reconectando…».
    /// </summary>
    public bool Conectado => _suscripcion is not null && !_liberado;

    /// <summary>
    /// Segundos que quedan de votación, interpolados con el reloj local desde
    /// el último latido. Nunca baja de cero y nunca sube por encima de lo que
    /// dijo el servidor: si el reloj local va adelantado, como mucho adelanta
    /// la cuenta, jamás la alarga.
    /// </summary>
    public double SegundosRestantes
    {
        get
        {
            if (Estado is not { } estado)
            {
                return 0d;
            }

            // Durante la cuenta atrás de preparación la votación todavía no ha
            // arrancado: el reloj se muestra lleno y parado, no a cero.
            if (estado.Fase == FaseVotacion.Preparacion)
            {
                return estado.SegundosTotales;
            }

            // El reloj solo corre cuando la votación está realmente abierta.
            if (estado.Fase != FaseVotacion.Abierta)
            {
                return estado.SegundosRestantes;
            }

            var transcurrido = (DateTime.UtcNow - _recibidoUtc).TotalSeconds;

            // Un desfase absurdo (hibernación, reloj cambiado) se ignora: se
            // confía en el valor del servidor hasta el siguiente latido.
            if (transcurrido is < 0d or > 5d)
            {
                return estado.SegundosRestantes;
            }

            return Math.Max(0d, estado.SegundosRestantes - transcurrido);
        }
    }

    /// <summary>Segundos restantes redondeados hacia arriba, que es lo que se pinta.</summary>
    public int SegundosRestantesEnteros =>
        (int)Math.Ceiling(Math.Max(0d, SegundosRestantes));

    /// <summary>
    /// Segundos que quedan de la cuenta atrás de PREPARACIÓN (el 5 → 0 previo
    /// a abrir la votación), interpolados con el reloj local desde el último
    /// latido.
    ///
    /// Va en una propiedad aparte porque el servidor manda los dos tiempos por
    /// separado: mientras la fase es Preparación, <c>SegundosRestantes</c> vale
    /// cero a propósito (la votación todavía no corre) y el tiempo de la cuenta
    /// atrás viaja en <c>SegundosPreparacion</c>. Confundirlos es lo que hacía
    /// que en los dispositivos solo se viera un «0» parpadeando.
    /// </summary>
    public double SegundosPreparacion
    {
        get
        {
            if (Estado is not { Fase: FaseVotacion.Preparacion } estado)
            {
                return 0d;
            }

            var transcurrido = (DateTime.UtcNow - _recibidoUtc).TotalSeconds;

            if (transcurrido is < 0d or > 5d)
            {
                return estado.SegundosPreparacion;
            }

            return Math.Max(0d, estado.SegundosPreparacion - transcurrido);
        }
    }

    /// <summary>Cuenta atrás de preparación redondeada hacia arriba, que es lo que se pinta.</summary>
    public int SegundosPreparacionEnteros =>
        (int)Math.Ceiling(Math.Max(0d, SegundosPreparacion));

    /// <summary>
    /// Queda poco tiempo: hay que pasar a rojo. Se recalcula con el tiempo
    /// interpolado para que el aviso no espere al siguiente latido.
    /// </summary>
    public bool EnAlertaRoja =>
        Estado is { Fase: FaseVotacion.Abierta, SegundosTotales: > 0 } estado
        && SegundosRestantes <= estado.UmbralAlertaSegundos;

    /// <summary>
    /// Se engancha al canal de la cata y pide el estado del instante. Es
    /// idempotente: llamarla dos veces con la misma cata no duplica nada.
    /// </summary>
    public async Task ConectarAsync(int cataId, CancellationToken ct = default)
    {
        if (CataId == cataId && Conectado)
        {
            return;
        }

        Desenganchar();

        CataId = cataId;
        _liberado = false;
        _suscripcion = _bus.Suscribir(cataId, this);

        _log.LogDebug("Pantalla enganchada al canal de la cata {CataId}.", cataId);

        await RefrescarAsync(ct);
    }

    /// <summary>
    /// Vuelve a pedir el estado al servidor. Se usa al conectar, al reconectar
    /// el circuito y cuando la pantalla sospecha que se ha perdido algo.
    /// </summary>
    public async Task RefrescarAsync(CancellationToken ct = default)
    {
        if (CataId is not int cataId)
        {
            return;
        }

        try
        {
            Estado = await _votacion.ObtenerEstadoAsync(cataId, ct);
            _recibidoUtc = DateTime.UtcNow;

            Progreso = await _votacion.ObtenerResumenAsync(cataId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(
                ex,
                "No se pudo recuperar el estado en vivo de la cata {CataId}.",
                cataId);
        }

        await AvisarAsync();
    }

    /// <inheritdoc />
    public async Task RecibirEstadoAsync(string evento, EstadoVivoDto estado)
    {
        UltimoEvento = evento;
        Estado = estado;
        _recibidoUtc = DateTime.UtcNow;

        await AvisarAsync();
    }

    /// <inheritdoc />
    public async Task RecibirTickAsync(EstadoVivoDto estado)
    {
        Estado = estado;
        _recibidoUtc = DateTime.UtcNow;

        await AvisarAsync();
    }

    /// <inheritdoc />
    public async Task RecibirResumenAsync(ProgresoCataDto progreso)
    {
        Progreso = progreso;

        await AvisarAsync();
    }

    private Task AvisarAsync()
    {
        if (_liberado)
        {
            return Task.CompletedTask;
        }

        return Cambio?.Invoke() ?? Task.CompletedTask;
    }

    private void Desenganchar()
    {
        _suscripcion?.Dispose();
        _suscripcion = null;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _liberado = true;
        Desenganchar();
        Cambio = null;
        CataId = null;

        return ValueTask.CompletedTask;
    }
}
