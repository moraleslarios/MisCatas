namespace MisCatas.Web.TiempoReal;

/* ---------------------------------------------------------------------------
   ClienteCartelera: lo que usan las pantallas EN ESPERA.

   El problema que resuelve: quien aguarda en la pantalla de inicio, o la
   televisión del escaparate cuando no hay nada en marcha, no conoce todavía
   el identificador de la cata que el administrador va a abrir. Por tanto no
   puede suscribirse al canal de esa cata, que es lo que hace ClienteCataHub.

   La cartelera es el canal sin destinatario: avisa de que la cata activa del
   sistema ha cambiado de situación y deja que cada pantalla vuelva a
   preguntar qué le toca mostrar, con SUS permisos. Deliberadamente no
   transporta datos de la cata: solo el nombre del evento y el identificador.

   Uso típico en un componente:

       @implements IDisposable
       @inject ClienteCartelera Cartelera

       protected override void OnInitialized()
       {
           Cartelera.Cambio += AlCambiarCarteleraAsync;
           Cartelera.Conectar();
       }

       private async Task AlCambiarCarteleraAsync(string evento, int cataId)
       {
           await RecargarAsync();
           await InvokeAsync(StateHasChanged);
       }

       public void Dispose()
       {
           Cartelera.Cambio -= AlCambiarCarteleraAsync;
           Cartelera.Dispose();
       }

   Es «Scoped»: una instancia por circuito de Blazor, o sea, una por pestaña.
   --------------------------------------------------------------------------- */

/// <summary>
/// Escucha de la cartelera: avisa a una pantalla en espera de que la cata
/// activa del sistema ha cambiado de situación.
/// </summary>
public sealed class ClienteCartelera : IOyenteCartelera, IDisposable
{
    private readonly BusCata _bus;
    private readonly ILogger<ClienteCartelera> _log;

    private IDisposable? _suscripcion;
    private bool _liberado;

    public ClienteCartelera(BusCata bus, ILogger<ClienteCartelera> log)
    {
        _bus = bus;
        _log = log;
    }

    /// <summary>
    /// Salta cuando la cata activa cambia de situación. El componente debe
    /// responder con <c>InvokeAsync(StateHasChanged)</c>, nunca con un
    /// <c>StateHasChanged</c> a secas: el aviso llega desde un hilo que no es
    /// el del renderizador.
    /// </summary>
    public event Func<string, int, Task>? Cambio;

    /// <summary>Nombre del último evento recibido, por si la pantalla quiere reaccionar.</summary>
    public string? UltimoEvento { get; private set; }

    /// <summary>Identificador de la cata del último aviso.</summary>
    public int? UltimaCataId { get; private set; }

    /// <summary>Está escuchando la cartelera.</summary>
    public bool Conectado => _suscripcion is not null && !_liberado;

    /// <summary>
    /// Se engancha a la cartelera. Es idempotente: llamarla dos veces no
    /// duplica la suscripción, de modo que se puede invocar sin miedo desde
    /// <c>OnParametersSet</c>.
    /// </summary>
    public void Conectar()
    {
        if (Conectado)
        {
            return;
        }

        _liberado = false;
        _suscripcion = _bus.SuscribirCartelera(this);
    }

    /// <inheritdoc />
    public async Task RecibirCarteleraAsync(string evento, int cataId)
    {
        if (_liberado)
        {
            return;
        }

        UltimoEvento = evento;
        UltimaCataId = cataId;

        _log.LogDebug("Cartelera: recibido «{Evento}» de la cata {CataId}.", evento, cataId);

        if (Cambio is { } manejador)
        {
            await manejador(evento, cataId);
        }
    }

    public void Dispose()
    {
        if (_liberado)
        {
            return;
        }

        _liberado = true;
        _suscripcion?.Dispose();
        _suscripcion = null;
        Cambio = null;
    }
}
