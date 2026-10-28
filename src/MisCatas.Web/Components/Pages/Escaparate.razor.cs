using Microsoft.AspNetCore.Components;
using MisCatas.Application.Comun;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;
using MisCatas.Web.Components.Shared;
using MisCatas.Web.TiempoReal;

namespace MisCatas.Web.Components.Pages;

/// <summary>
/// Pantalla única del modo escaparate: una televisión horizontal que proyecta
/// el estado de la cata sin ningún control interactivo.
/// El tiempo y la fase los manda el servidor por <see cref="ClienteCataHub"/>,
/// de modo que las dos televisiones y los diez móviles van al unísono.
/// </summary>
public partial class Escaparate : IAsyncDisposable
{
    /// <summary>Número de puestos que se listan en la revelación.</summary>
    private const int PuestosVisibles = 5;

    /// <summary>
    /// Lo que dura la botella del ganador a pantalla completa antes de dejar
    /// sitio a la clasificación. Doce segundos es lo que tarda la sala en
    /// aplaudir y mirar la etiqueta; más tiempo y la gente empieza a pedir los
    /// números.
    /// </summary>
    private const int SegundosGala = 12;

    /// <summary>
    /// Corta la espera de la gala cuando la televisión cambia de pantalla o se
    /// apaga el circuito, para no apagar un cartel que ya no existe.
    /// </summary>
    private CancellationTokenSource? _gala;

    /// <summary>
    /// Está puesta la botella del ganador a pantalla completa. Es un estado
    /// local de esta televisión: no se sincroniza con nada, porque es
    /// puramente escenográfico.
    /// </summary>
    private bool _enGala;

    /// <summary>
    /// Alguien ha pulsado «volver al inicio» desde la pantalla de resultados.
    /// La cata sigue finalizada en la base de datos, lo único que cambia es
    /// que esta pantalla deja de proyectar el veredicto.
    /// </summary>
    private bool _vueltaAlInicio;

    /// <summary>
    /// Hay un veredicto guardado que esta televisión ha apartado a mano. Se
    /// ofrece volver a él para que «volver al inicio» no sea un camino sin
    /// retorno en una sala sin teclado.
    /// </summary>
    private bool HayVeredictoApartado => _vueltaAlInicio && Datos.Pantalla == PantallaEscaparate.Revelacion;

    [Inject]
    private IServicioParticipacion Servicio { get; set; } = default!;

    /// <summary>Conexión en vivo con la mesa (una por circuito de Blazor).</summary>
    [Inject]
    private ClienteCataHub Vivo { get; set; } = default!;

    /// <summary>
    /// Escucha de la cartelera. Mientras la televisión está en espera no hay
    /// ninguna cata a la que engancharse, así que sin esto no se enteraría
    /// nunca de que el administrador acaba de abrir una.
    /// </summary>
    [Inject]
    private ClienteCartelera Cartelera { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    /// <summary>
    /// Sabe por qué dirección pueden entrar los invitados. Hace falta porque la
    /// televisión puede estar entrando por «localhost» (si corre en el mismo
    /// equipo que la aplicación), y eso en un móvil no lleva a ninguna parte.
    /// </summary>
    [Inject]
    private IServicioDireccionSitio Direccion { get; set; } = default!;

    /// <summary>
    /// Dirección de entrada ya resuelta, cacheada por circuito: no cambia
    /// mientras la televisión está encendida y esta pantalla se repinta muchas
    /// veces por segundo.
    /// </summary>
    private DireccionSitio? _sitio;

    private DireccionSitio Sitio => _sitio ??= Direccion.Resolver(Navegacion.BaseUri);

    /// <summary>
    /// Dirección completa, con <c>http://</c> y todo, para meterla en el QR.
    /// </summary>
    private string? UrlEntrada => Sitio.Raiz;

    /// <summary>
    /// Dirección que tienen que teclear los participantes en el móvil, sin el
    /// <c>http://</c> delante para que se lea más corta en la pantalla.
    /// </summary>
    private string DireccionEntrada
    {
        get
        {
            // Si no hay forma de saberla, se cae a la de la propia navegación:
            // es lo que había antes y al menos dice algo.
            var texto = UrlEntrada ?? Navegacion.BaseUri;

            if (!Uri.TryCreate(texto, UriKind.Absolute, out var uri))
            {
                return texto;
            }

            var puerto = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";

            return $"{uri.Host}{puerto}";
        }
    }

    private CataEscaparateDto Datos { get; set; } = CataEscaparateDto.EnEspera();

    private EstadoVivoDto? Estado => Vivo.Estado;

    private FaseVotacion Fase => Estado?.Fase ?? FaseVotacion.Inactiva;

    /// <summary>Progreso en directo si el hub ya lo mandó; si no, el de la carga.</summary>
    private ProgresoCataDto Progreso =>
        Vivo.Progreso ?? Datos.Progreso ?? new ProgresoCataDto(0, 0, 0, null, 0, 0, null, null);

    private IReadOnlyList<VinoReveladoDto> Clasificacion =>
        Datos.Resultado is null
            ? []
            : [.. Datos.Resultado.Clasificacion.Take(PuestosVisibles)];

    private int SegundosTotales => Estado?.SegundosTotales ?? Datos.TiempoVotoSegundos;

    private int PorcentajeAlerta => Estado?.PorcentajeAlertaRoja ?? Datos.PorcentajeAlertaRoja;

    private int SegundosRestantes => Vivo.SegundosRestantesEnteros;

    /// <summary>
    /// Cuenta atrás de preparación (5 → 0). Es un tiempo distinto del de la
    /// votación, que durante esta fase todavía no ha arrancado.
    /// </summary>
    private int SegundosPreparacion => Vivo.SegundosPreparacionEnteros;

    /// <summary>Cuenta atrás previa de 5 a 0, a pantalla completa.</summary>
    private bool EnPreparacion => Fase == FaseVotacion.Preparacion;

    /// <summary>El administrador ha congelado el reloj de este vino.</summary>
    private bool EnPausa => Fase == FaseVotacion.Pausada;

    /// <summary>El reloj grande solo corre mientras el servidor acepta votos.</summary>
    private bool VotacionAbierta => Fase == FaseVotacion.Abierta;

    private int? NumeroVinoActual => Estado?.NumeroVinoActual ?? Datos.NumeroVinoActual;

    private bool SinConexion => Datos.CataId is not null && !Vivo.Conectado;

    /// <summary>
    /// Rótulo del mural de votantes. Mientras falta gente se dice cuántos
    /// quedan, que es la información que la sala mira de reojo; cuando están
    /// todos se celebra, porque esa es la señal de que el vino ya puede
    /// cerrarse.
    /// </summary>
    private string TituloMural => Progreso.Faltan == 0
        ? "Han votado todos"
        : Progreso.Faltan == 1
            ? "Falta 1 por votar"
            : $"Faltan {Progreso.Faltan} por votar";

    /// <summary>
    /// Pantalla que de verdad se pinta. Coincide con la que manda el servicio
    /// salvo cuando alguien ha pulsado «volver al inicio»: entonces esta
    /// televisión se queda en la bienvenida aunque el veredicto siga
    /// disponible, hasta que ocurra algo nuevo en la cata.
    /// </summary>
    private PantallaEscaparate Pantalla => _vueltaAlInicio && Datos.Pantalla == PantallaEscaparate.Revelacion
        ? PantallaEscaparate.Espera
        : Datos.Pantalla;

    /// <summary>
    /// Botella del ganador a pantalla completa. Solo en la revelación y solo
    /// mientras dure la gala.
    /// </summary>
    private bool EnGala => _enGala
        && Pantalla == PantallaEscaparate.Revelacion
        && Datos.Resultado?.Ganador is not null;

    /// <summary>
    /// Etiqueta real del vino para la botella virtual. Si no hay etiqueta
    /// cargada se cae a la foto, y si tampoco la hay se devuelve null para que
    /// la botella dibuje el hueco con el número de orden.
    /// </summary>
    private static string? UrlEtiqueta(VinoReveladoDto vino) => vino switch
    {
        { TieneEtiqueta: true } => $"/imagenes/vinos/{vino.VinoId}/etiqueta",
        { TieneFoto: true } => $"/imagenes/vinos/{vino.VinoId}/foto",
        _ => null
    };

    protected override async Task OnInitializedAsync()
    {
        Vivo.Cambio += AlCambiarEnVivoAsync;

        // La cartelera se engancha ANTES de la primera lectura: si el
        // administrador abre la cata justo en ese hueco, el aviso ya tiene a
        // quién llegar y la televisión no se queda colgada en la espera.
        Cartelera.Cambio += AlCambiarCarteleraAsync;
        Cartelera.Conectar();

        await RecargarAsync();
        await SincronizarCanalAsync();
    }

    private async Task RecargarAsync()
    {
        var anterior = Datos.Pantalla;
        Datos = await Servicio.ObtenerEscaparateAsync();

        // Cualquier cosa que no sea seguir revelando lo mismo cancela la
        // vuelta al inicio: si el administrador reabre la cata o arranca otra,
        // la televisión tiene que volver a hacer su trabajo.
        if (Datos.Pantalla != PantallaEscaparate.Revelacion)
        {
            _vueltaAlInicio = false;
        }

        // La gala se dispara UNA vez, al entrar en la revelación. Si la
        // pantalla ya era la de revelación no se reinicia, porque entonces
        // cada refresco volvería a tapar la clasificación.
        if (Datos.Pantalla == PantallaEscaparate.Revelacion && anterior != PantallaEscaparate.Revelacion)
        {
            ArrancarGala();
        }
        else if (Datos.Pantalla != PantallaEscaparate.Revelacion)
        {
            PararGala();
        }
    }

    /// <summary>
    /// Engancha la pantalla al canal de la cata que toca proyectar. Se llama
    /// tras CADA relectura porque la cata elegida puede cambiar: la televisión
    /// arranca anunciando la próxima, luego esa misma se abre, y al terminar
    /// el servicio puede pasar a revelar otra. <c>ConectarAsync</c> es
    /// idempotente y desengancha la anterior, así que repetirla no cuesta.
    /// </summary>
    private async Task SincronizarCanalAsync()
    {
        if (Datos.CataId is int cataId && Vivo.CataId != cataId)
        {
            await Vivo.ConectarAsync(cataId);
        }
    }

    private async Task AlCambiarEnVivoAsync()
    {
        if (RequiereRelectura(Vivo.UltimoEvento))
        {
            await RecargarAsync();
            await SincronizarCanalAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Aviso de la cartelera: ha cambiado la cata activa del sistema. No se
    /// mira el identificador que llega, se vuelve a preguntar al servicio qué
    /// toca proyectar, que es quien decide con qué prioridad.
    /// </summary>
    private async Task AlCambiarCarteleraAsync(string evento, int cataId)
    {
        await RecargarAsync();
        await SincronizarCanalAsync();
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Los ticks del reloj no tocan la base de datos; solo los cambios de fase
    /// obligan a releer la pantalla que toca proyectar.
    /// </summary>
    private static bool RequiereRelectura(string? evento) => evento is
        EventosCata.CataAbierta or
        EventosCata.CuentaAtrasIniciada or
        EventosCata.VotacionAbierta or
        EventosCata.VotacionCerrada or
        EventosCata.VotoReseteado or
        EventosCata.CataFinalizada or
        EventosCata.CataReabierta or
        EventosCata.RevelacionIniciada or
        EventosCata.RevelacionOcultada;

    private static string ClasePodio(VinoReveladoDto vino) => vino.Posicion switch
    {
        1 => "tv-podio-fila tv-podio-fila--oro",
        2 => "tv-podio-fila tv-podio-fila--plata",
        3 => "tv-podio-fila tv-podio-fila--bronce",
        _ => "tv-podio-fila",
    };

    private static string TextoMedia(VinoReveladoDto vino) =>
        vino.NotaMedia is null ? "—" : vino.NotaMedia.Value.ToString("0.0");

    public async ValueTask DisposeAsync()
    {
        PararGala();

        Cartelera.Cambio -= AlCambiarCarteleraAsync;
        Cartelera.Dispose();

        Vivo.Cambio -= AlCambiarEnVivoAsync;
        await Vivo.DisposeAsync();
    }

    /// <summary>
    /// Pone la botella del ganador a pantalla completa y programa su retirada.
    /// La espera va en segundo plano a propósito: el resto de la pantalla no
    /// puede quedarse bloqueado doce segundos.
    /// </summary>
    private void ArrancarGala()
    {
        PararGala();

        _enGala = true;
        var cts = new CancellationTokenSource();
        _gala = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(SegundosGala), cts.Token);
                _enGala = false;
                await InvokeAsync(StateHasChanged);
            }
            catch (OperationCanceledException)
            {
                // La pantalla cambió antes de tiempo: no hay nada que retirar.
            }
        });
    }

    private void PararGala()
    {
        _enGala = false;
        _gala?.Cancel();
        _gala?.Dispose();
        _gala = null;
    }

    /// <summary>
    /// «Volver al inicio» desde los resultados. No se toca la cata: solo esta
    /// televisión deja de proyectar el veredicto y vuelve a la bienvenida.
    /// </summary>
    private void VolverAlInicio()
    {
        PararGala();
        _vueltaAlInicio = true;
    }

    /// <summary>Repetir la gala a mano, que siempre la piden otra vez.</summary>
    private void RepetirGala()
    {
        _vueltaAlInicio = false;
        ArrancarGala();
    }

    /// <summary>
    /// Nota media de toda la cata, calculada sobre las medias de cada vino:
    /// es lo que la sala entiende por «cómo ha salido la noche».
    /// </summary>
    private decimal? MediaCata
    {
        get
        {
            var conNota = (Datos.Resultado?.Clasificacion ?? [])
                .Where(v => v.NotaMedia is not null)
                .ToList();

            return conNota.Count == 0 ? null : conNota.Average(v => v.NotaMedia!.Value);
        }
    }

    private string TextoMediaCata => MediaCata is null ? "—" : MediaCata.Value.ToString("0.0");

    /// <summary>Papeletas contadas en toda la cata.</summary>
    private int TotalVotos => Datos.Resultado?.Clasificacion.Sum(v => v.VotosContados) ?? 0;

    /// <summary>
    /// Tira de botellas del recorrido de la cata, todavía a ciegas: una por
    /// vino, con su número de orden y si está catado, en la mesa o pendiente.
    /// Nunca lleva etiqueta ni nota, que la cata sigue en marcha.
    /// </summary>
    private IReadOnlyList<PiezaVitrina> PiezasRecorrido
    {
        get
        {
            var total = Progreso.TotalVinos;
            if (total <= 0)
            {
                return [];
            }

            var piezas = new List<PiezaVitrina>(total);
            for (var numero = 1; numero <= total; numero++)
            {
                var esActual = numero == NumeroVinoActual;
                var subtitulo = esActual
                    ? "En la mesa"
                    : numero <= Progreso.VinosCerrados ? "Catado" : "Pendiente";

                piezas.Add(new PiezaVitrina(
                    Titulo: $"Nº {numero}",
                    Subtitulo: subtitulo,
                    NumeroOrden: numero,
                    Destacada: esActual));
            }

            return piezas;
        }
    }

    /// <summary>
    /// Estantería del veredicto: todos los vinos con su etiqueta real, su
    /// medallón de puesto y su nota media. La foto de familia de la cata.
    /// </summary>
    private IReadOnlyList<PiezaVitrina> PiezasVeredicto =>
        Datos.Resultado is null
            ? []
            : [.. Datos.Resultado.Clasificacion.Select(vino => new PiezaVitrina(
                Titulo: vino.NombreConAnio,
                Subtitulo: string.IsNullOrWhiteSpace(vino.Bodega) ? vino.Dueno : vino.Bodega,
                Nota: TextoMedia(vino),
                Detalle: $"Lo trajo {vino.Dueno}",
                NumeroOrden: vino.NumeroOrden,
                UrlEtiqueta: UrlEtiqueta(vino),
                Puesto: vino.Posicion,
                Destacada: vino.Posicion == 1))];
}
