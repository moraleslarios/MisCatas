using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MisCatas.Application.Catas;
using MisCatas.Application.Comun;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;
using MisCatas.Application.Vinos;
using MisCatas.Domain.Enums;
using MisCatas.Web.Seguridad;
using MisCatas.Web.TiempoReal;

namespace MisCatas.Web.Components.Pages.Admin;

/* ---------------------------------------------------------------------------
   Mesa de control: el mando real de la cata.

   Tres cosas que esta pantalla NO hace, a propósito:

   1) NO lleva su propio temporizador. El tiempo que se ve sale del estado que
      manda el servidor; el cliente solo lo interpola entre latidos. Si el
      organizador recarga la página, el reloj sigue donde estaba de verdad.

   2) NO finaliza la cata por su cuenta. Aunque se agote el último vino, la
      cata sigue en curso hasta que el organizador pulsa «Finalizar».

   3) NO decide quién manda. Eso lo valida el servidor con AdminControlaId: si
      entra un segundo administrador, ve exactamente lo mismo pero en lectura.
      Aquí solo se refleja esa decisión para no ofrecer botones que fallarían.
   --------------------------------------------------------------------------- */

/// <summary>
/// Mesa de control del organizador: estado de la cata en directo y mando para
/// gobernarla (abrir, lanzar vino, cerrar, resetear, finalizar y revelar).
/// </summary>
public partial class CataControl : IAsyncDisposable
{
    [Inject]
    private IServicioCatas ServicioCatas { get; set; } = default!;

    [Inject]
    private IServicioParticipacion ServicioParticipacion { get; set; } = default!;

    [Inject]
    private IServicioVotacion ServicioVotacion { get; set; } = default!;

    [Inject]
    private IServicioVinos ServicioVinos { get; set; } = default!;

    [Inject]
    private ClienteCataHub Vivo { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? EstadoAutenticacion { get; set; }

    [Parameter]
    public int Id { get; set; }

    private CataDetalleDto? Detalle { get; set; }

    private ProgresoCataDto Progreso { get; set; } =
        new ProgresoCataDto(0, 0, 0, null, 0, 0, null, null);

    private bool Cargando { get; set; } = true;

    private bool Trabajando { get; set; }

    private string? Error { get; set; }

    private string? Mensaje { get; set; }

    /// <summary>Identificador del administrador que está mirando la pantalla.</summary>
    private int? MiAdminId { get; set; }

    /// <summary>Confirmación pendiente para una acción destructiva.</summary>
    private ConfirmacionPendiente? Confirmacion { get; set; }

    // --- Ficha de reconocimiento -------------------------------------------

    /// <summary>Si la hoja con la botella real está en pantalla.</summary>
    private bool FichaVisible { get; set; }

    /// <summary>Botella que se está mostrando en la hoja de reconocimiento.</summary>
    private VinoDto? FichaVino { get; set; }

    /// <summary>Posición en el recorrido del vino mostrado en la hoja.</summary>
    private int? FichaOrden { get; set; }

    /// <summary>
    /// Abre la hoja con la botella real de un vino del recorrido. En la mesa
    /// los vinos van a ciegas, y el organizador necesita saber qué sirve antes
    /// de lanzarlo; se hace en una hoja superpuesta y no navegando, para no
    /// tirar la conexión en vivo ni perder el reloj.
    /// </summary>
    private async Task AbrirFichaAsync(CataVinoDto vino)
    {
        Error = null;
        Mensaje = null;

        var ficha = await ServicioVinos.ObtenerAsync(vino.VinoId);

        if (ficha is null)
        {
            Error = "No he podido cargar la ficha de ese vino.";
            return;
        }

        FichaVino = ficha;
        FichaOrden = vino.Orden;
        FichaVisible = true;
    }

    /// <summary>Abre la ficha del vino que toca lanzar ahora.</summary>
    private Task AbrirFichaSiguienteAsync() =>
        SiguienteVino is { } vino ? AbrirFichaAsync(vino) : Task.CompletedTask;

    private void CerrarFicha()
    {
        FichaVisible = false;
        FichaVino = null;
        FichaOrden = null;
    }

    /// <summary>Cierra la hoja y lanza de inmediato el vino que mostraba.</summary>
    private async Task LanzarDesdeFichaAsync()
    {
        if (FichaOrden is not int orden)
        {
            return;
        }

        CerrarFicha();
        await LanzarVinoAsync(orden);
    }

    // --- Estado en vivo -----------------------------------------------------

    private EstadoVivoDto? Estado => Vivo.Estado;

    private FaseVotacion Fase => Estado?.Fase ?? FaseVotacion.Inactiva;

    private EstadoCata EstadoActual => Detalle?.Cata.Estado ?? EstadoCata.Borrador;

    /// <summary>
    /// Este administrador tiene el mando. Si nadie lo ha tomado todavía, el
    /// primero que actúe se queda con él; por eso «sin dueño» cuenta como sí.
    /// </summary>
    private bool TengoElMando =>
        MiAdminId is int mio
        && (Estado?.AdminControlaId is null || Estado.AdminControlaId == mio);

    /// <summary>
    /// Hay otro administrador al mando: esta pantalla pasa a solo lectura.
    /// </summary>
    private bool SoloLectura => !TengoElMando;

    /// <summary>Número de orden del vino que está en juego ahora mismo.</summary>
    private int? OrdenActual => Estado?.NumeroVinoActual ?? Progreso.NumeroVinoActual;

    private CataVinoDto? VinoActual =>
        Detalle?.Vinos.FirstOrDefault(v => v.Orden == OrdenActual);

    /// <summary>
    /// Siguiente vino que toca lanzar: el primero que aún no está cerrado. Si
    /// están todos cerrados devuelve <c>null</c> y el mando ofrece finalizar.
    /// </summary>
    private CataVinoDto? SiguienteVino =>
        Detalle?.Vinos
            .Where(v => v.EstadoVotacion != EstadoVotacion.Cerrada)
            .OrderBy(v => v.Orden)
            .FirstOrDefault();

    /// <summary>Quedan vinos por cerrar.</summary>
    private bool QuedanVinos => SiguienteVino is not null;

    /// <summary>Segundos que se pintan en el reloj, interpolados sobre el último latido.</summary>
    private int SegundosRestantes => Vivo.SegundosRestantesEnteros;

    /// <summary>Cuenta atrás de preparación (5 → 0), que es un tiempo aparte.</summary>
    private int SegundosPreparacion => Vivo.SegundosPreparacionEnteros;

    /// <summary>
    /// Cifra grande del reloj: durante la preparación muestra el 5 → 0 y en
    /// cuanto se abre la votación pasa al tiempo de voto.
    /// </summary>
    private int CifraReloj =>
        Fase == FaseVotacion.Preparacion ? SegundosPreparacion : SegundosRestantes;

    /// <summary>El reloj entra en rojo: queda poco tiempo.</summary>
    private bool EnAlertaRoja => Vivo.EnAlertaRoja;

    /// <summary>Porcentaje de tiempo consumido, para la barra del reloj.</summary>
    private int PorcentajeTiempo
    {
        get
        {
            if (Estado is not { SegundosTotales: > 0 } estado)
            {
                return 0;
            }

            var consumido = (estado.SegundosTotales - Vivo.SegundosRestantes)
                / estado.SegundosTotales * 100d;

            return (int)Math.Clamp(Math.Round(consumido), 0d, 100d);
        }
    }

    // --- Qué botones se pueden pulsar ---------------------------------------

    /// <summary>Condición común: tengo el mando y no hay otra acción en vuelo.</summary>
    private bool Mandable => TengoElMando && !Trabajando;

    private bool PuedeAbrir =>
        Mandable && EstadoActual == EstadoCata.Borrador;

    private bool PuedeLanzar =>
        Mandable
        && EstadoActual is EstadoCata.Abierta or EstadoCata.EnCurso
        && Fase is FaseVotacion.Inactiva or FaseVotacion.Cerrada
        && QuedanVinos;

    private bool PuedeCerrar =>
        Mandable && Fase is FaseVotacion.Preparacion or FaseVotacion.Abierta or FaseVotacion.Pausada;

    /// <summary>Solo se pausa una votación que ya está recogiendo votos.</summary>
    private bool PuedePausar => Mandable && Fase == FaseVotacion.Abierta;

    /// <summary>Solo se reanuda lo que está congelado.</summary>
    private bool PuedeReanudar => Mandable && Fase == FaseVotacion.Pausada;

    private bool PuedeResetearVino =>
        Mandable
        && EstadoActual == EstadoCata.EnCurso
        && Fase is FaseVotacion.Inactiva or FaseVotacion.Cerrada
        && OrdenActual is not null;

    private bool PuedeFinalizar =>
        Mandable
        && EstadoActual is EstadoCata.Abierta or EstadoCata.EnCurso
        && Fase is not (FaseVotacion.Preparacion or FaseVotacion.Abierta or FaseVotacion.Pausada);

    private bool PuedeReabrir =>
        Mandable && EstadoActual == EstadoCata.Finalizada;

    private bool PuedeRevelar =>
        Mandable && EstadoActual == EstadoCata.Finalizada && Estado?.RevelacionActiva != true;

    private bool PuedeOcultarRevelacion =>
        Mandable && EstadoActual == EstadoCata.Finalizada && Estado?.RevelacionActiva == true;

    /// <summary>
    /// Se puede barajar el recorrido: hay mando, la cata aún no ha empezado a
    /// rodar y ningún vino se ha lanzado. Cambiar el orden con la cata en
    /// marcha dejaría los votos ya emitidos apuntando a otra posición, así que
    /// esto solo vale antes del primer vino.
    /// </summary>
    private bool PuedeOrdenarAlAzar =>
        Mandable
        && EstadoActual is EstadoCata.Borrador or EstadoCata.Abierta
        && Fase == FaseVotacion.Inactiva
        && Detalle is { } detalle
        && detalle.Vinos.Count > 1
        && detalle.Vinos.All(v => v.EstadoVotacion == EstadoVotacion.Pendiente);

    /// <summary>
    /// Se puede saltar a este vino: hay mando, la cata está viva, no hay una
    /// votación en marcha y no es el vino que ya está en juego.
    /// </summary>
    private bool PuedeSaltarA(CataVinoDto vino) =>
        Mandable
        && EstadoActual is EstadoCata.Abierta or EstadoCata.EnCurso
        && Fase is FaseVotacion.Inactiva or FaseVotacion.Cerrada
        && vino.Orden != OrdenActual;

    /// <summary>
    /// Igual que <see cref="PuedeSaltarA(CataVinoDto)"/>, pero buscando el vino
    /// por su posición. Lo usa la hoja de reconocimiento, que solo guarda el
    /// número de orden del vino que está enseñando.
    /// </summary>
    private bool PuedeSaltarAOrden(int orden) =>
        Detalle?.Vinos.FirstOrDefault(v => v.Orden == orden) is { } vino
        && PuedeSaltarA(vino);

    /// <summary>Texto del botón de lanzar, que cambia según el vino que toque.</summary>
    private string TextoLanzar => SiguienteVino is { } vino
        ? $"Lanzar vino nº {vino.Orden}"
        : "No quedan vinos";

    private string TextoEstadoVinoActual => Fase switch
    {
        FaseVotacion.Preparacion => "Cuenta atrás en marcha",
        FaseVotacion.Abierta => "Votación abierta",
        FaseVotacion.Pausada => "Votación en pausa",
        FaseVotacion.Cerrada => "Votación cerrada",
        _ => Progreso.EstadoVinoActual switch
        {
            EstadoVotacion.Abierta => "Votación abierta",
            EstadoVotacion.Cerrada => "Votación cerrada",
            EstadoVotacion.Pendiente => "A la espera",
            _ => "Sin vino en juego",
        },
    };

    private string TextoFase => Fase switch
    {
        FaseVotacion.Preparacion => "Preparados…",
        FaseVotacion.Abierta => "Votando",
        FaseVotacion.Pausada => "Pausada",
        FaseVotacion.Cerrada => "Votación cerrada",
        _ => "En reposo",
    };

    // --- Ciclo de vida ------------------------------------------------------

    protected override async Task OnInitializedAsync()
    {
        if (EstadoAutenticacion is not null)
        {
            var autenticacion = await EstadoAutenticacion;
            MiAdminId = autenticacion.User.ObtenerUsuarioId();
        }

        // El bus avisa desde un hilo ajeno al renderizador: de ahí el
        // InvokeAsync del manejador.
        Vivo.Cambio += AlCambiarEnVivoAsync;
    }

    protected override async Task OnParametersSetAsync()
    {
        Cargando = true;
        Error = null;

        await RecargarAsync();

        // Engancharse al canal va DESPUÉS de cargar el detalle: así el primer
        // repintado ya tiene vinos y participantes con los que pintar.
        await Vivo.ConectarAsync(Id);

        Cargando = false;
    }

    /// <summary>
    /// Relee de la base de datos lo que no viaja por el canal en vivo: el
    /// estado de la cata, los vinos y los participantes.
    /// </summary>
    private async Task RecargarAsync()
    {
        Detalle = await ServicioCatas.ObtenerDetalleAsync(Id);

        if (Detalle is not null)
        {
            Progreso = await ServicioParticipacion.ObtenerProgresoAsync(Id)
                ?? new ProgresoCataDto(0, 0, 0, null, Detalle.Vinos.Count, 0, null, null);
        }
    }

    /// <summary>
    /// Llega un aviso del servidor. Los ticks solo repintan; los eventos que
    /// cambian el curso de la cata obligan además a releer vinos y estado,
    /// porque eso no viaja en el estado en vivo.
    /// </summary>
    private async Task AlCambiarEnVivoAsync()
    {
        if (Vivo.Progreso is { } progreso)
        {
            Progreso = progreso;
        }

        if (RequiereRelectura(Vivo.UltimoEvento))
        {
            await RecargarAsync();
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Eventos tras los que el detalle de la cata ya no sirve: han cambiado el
    /// estado de la cata o el de los vinos.
    /// </summary>
    private static bool RequiereRelectura(string? evento) => evento is
        EventosCata.CataAbierta or
        EventosCata.VotacionAbierta or
        EventosCata.VotacionCerrada or
        EventosCata.VotoReseteado or
        EventosCata.CataFinalizada or
        EventosCata.CataReabierta;

    public async ValueTask DisposeAsync()
    {
        Vivo.Cambio -= AlCambiarEnVivoAsync;
        await Vivo.DisposeAsync();
    }

    // --- Acciones del mando -------------------------------------------------

    /// <summary>
    /// Envoltorio común de todas las acciones: bloquea el mando mientras la
    /// orden está en vuelo (para que un doble toque no la mande dos veces),
    /// traduce el resultado a aviso y relee el detalle al terminar.
    /// </summary>
    private async Task EjecutarAsync(Func<int, Task<Resultado>> accion, string exito)
    {
        if (MiAdminId is not int adminId || Trabajando)
        {
            return;
        }

        Trabajando = true;
        Error = null;
        Mensaje = null;

        try
        {
            var resultado = await accion(adminId);

            if (resultado.Exito)
            {
                Mensaje = exito;
            }
            else
            {
                Error = resultado.Error;
            }

            await RecargarAsync();
        }
        finally
        {
            Trabajando = false;
        }
    }

    private Task AbrirCataAsync() =>
        EjecutarAsync(
            admin => ServicioVotacion.AbrirCataAsync(Id, admin),
            "Cata abierta. Ya pueden entrar los participantes.");

    private Task LanzarSiguienteAsync()
    {
        if (SiguienteVino is not { } vino)
        {
            return Task.CompletedTask;
        }

        return LanzarVinoAsync(vino.Orden);
    }

    /// <summary>
    /// Lanza un vino concreto, no necesariamente el siguiente. Sirve para
    /// repetir uno ya cerrado o para saltarse el orden si en la mesa se ha
    /// servido otro por error.
    /// </summary>
    private Task LanzarVinoAsync(int orden) =>
        EjecutarAsync(
            admin => ServicioVotacion.LanzarVotacionAsync(Id, orden, admin),
            $"Lanzado el vino nº {orden}.");

    private Task CerrarVotacionAsync() =>
        EjecutarAsync(
            admin => ServicioVotacion.CerrarVotacionAsync(Id, admin),
            "Votación cerrada.");

    private Task PausarVotacionAsync() =>
        EjecutarAsync(
            admin => ServicioVotacion.PausarVotacionAsync(Id, admin),
            "Votación en pausa. El reloj está congelado.");

    private Task ReanudarVotacionAsync() =>
        EjecutarAsync(
            admin => ServicioVotacion.ReanudarVotacionAsync(Id, admin),
            "Votación reanudada.");

    private Task ActivarRevelacionAsync() =>
        EjecutarAsync(
            admin => ServicioVotacion.ActivarRevelacionAsync(Id, admin),
            "Revelación en pantalla.");

    private Task OcultarRevelacionAsync() =>
        EjecutarAsync(
            admin => ServicioVotacion.OcultarRevelacionAsync(Id, admin),
            "Revelación oculta.");

    // --- Acciones que piden confirmación ------------------------------------

    /// <summary>
    /// Resetear borra votos: nunca se hace a la primera. Se pide confirmación
    /// diciendo claramente qué se pierde.
    /// </summary>
    private void PedirResetearVino()
    {
        if (OrdenActual is not int orden)
        {
            return;
        }

        Confirmacion = new ConfirmacionPendiente(
            $"¿Resetear el vino nº {orden}?",
            "Se borrarán TODOS los votos de ese vino y volverá a quedar pendiente. "
                + "Esta acción no se puede deshacer.",
            "Sí, borrar los votos",
            EsPeligro: true,
            () => EjecutarAsync(
                admin => ServicioVotacion.ResetearVinoAsync(Id, orden, admin),
                $"Vino nº {orden} reseteado."));
    }

    /// <summary>
    /// Borra el voto de una sola persona en el vino en juego, para cuando
    /// alguien se equivoca de casilla y hay que dejarle votar otra vez.
    /// </summary>
    private void PedirResetearVoto(int usuarioId, string nombre)
    {
        if (OrdenActual is not int orden)
        {
            return;
        }

        Confirmacion = new ConfirmacionPendiente(
            $"¿Borrar el voto de {nombre}?",
            $"Se borrará su voto del vino nº {orden} y podrá volver a votarlo. "
                + "El resto de votos no se toca.",
            "Sí, borrar su voto",
            EsPeligro: true,
            () => EjecutarAsync(
                admin => ServicioVotacion.ResetearVotoUsuarioAsync(Id, orden, usuarioId, admin),
                $"Voto de {nombre} borrado."));
    }

    private void PedirFinalizar() =>
        Confirmacion = new ConfirmacionPendiente(
            "¿Finalizar la cata?",
            "Se destaparán los datos de todos los vinos y se publicará la clasificación. "
                + "Podrás reabrirla, pero mientras esté finalizada todo el mundo verá los resultados.",
            "Finalizar la cata",
            EsPeligro: false,
            () => EjecutarAsync(
                admin => ServicioVotacion.FinalizarCataAsync(Id, admin),
                "Cata finalizada. Ya se pueden ver los resultados."));

    private void PedirReabrir() =>
        Confirmacion = new ConfirmacionPendiente(
            "¿Reabrir la cata?",
            "Los datos de los vinos se volverán a ocultar y la clasificación dejará de verse. "
                + "Los votos ya emitidos se conservan.",
            "Reabrir la cata",
            EsPeligro: true,
            () => EjecutarAsync(
                admin => ServicioVotacion.ReabrirCataAsync(Id, admin),
                "Cata reabierta. Los datos vuelven a estar ocultos."));

    /// <summary>
    /// Barajar cambia el recorrido entero, así que se avisa antes: si el
    /// organizador ya tenía las botellas colocadas en una mesa siguiendo el
    /// orden anterior, tendrá que volver a mirarlo.
    /// </summary>
    private void PedirOrdenarAlAzar() =>
        Confirmacion = new ConfirmacionPendiente(
            "¿Ordenar los vinos al azar?",
            "Se cambiará el orden en que se van a lanzar los vinos de esta cata. "
                + "Si ya habías colocado las botellas, comprueba el nuevo recorrido antes de empezar.",
            "Sí, barajar",
            EsPeligro: false,
            () => EjecutarAsync(
                _ => ServicioCatas.OrdenarVinosAlAzarAsync(Id),
                "Recorrido barajado. Este es el nuevo orden."));

    private async Task ConfirmarAsync()
    {
        var pendiente = Confirmacion;
        Confirmacion = null;

        if (pendiente is not null)
        {
            await pendiente.Accion();
        }
    }

    private void CancelarConfirmacion() => Confirmacion = null;

    // --- Etiquetas ----------------------------------------------------------

    private static string NombreEstado(EstadoCata estado) => estado switch
    {
        EstadoCata.Borrador => "Borrador",
        EstadoCata.Abierta => "Abierta",
        EstadoCata.EnCurso => "En curso",
        _ => "Finalizada",
    };

    private static string TextoEstadoVotacion(EstadoVotacion estado) => estado switch
    {
        EstadoVotacion.Abierta => "Abierta",
        EstadoVotacion.Cerrada => "Cerrada",
        _ => "Pendiente",
    };

    private static string ClasePastillaEstado(EstadoCata estado) => estado switch
    {
        EstadoCata.Abierta => "mc-pastilla-abierta",
        EstadoCata.EnCurso => "mc-pastilla-encurso",
        EstadoCata.Finalizada => "mc-pastilla-finalizada",
        _ => "mc-pastilla-borrador",
    };

    private static string ClasePastillaVotacion(EstadoVotacion estado) => estado switch
    {
        EstadoVotacion.Abierta => "control-pastilla control-pastilla--abierta",
        EstadoVotacion.Cerrada => "control-pastilla control-pastilla--cerrada",
        _ => "control-pastilla",
    };

    private string ClaseHito(CataVinoDto vino)
    {
        var clases = "control-hito";

        if (vino.EstadoVotacion == EstadoVotacion.Cerrada)
        {
            clases += " control-hito--cerrado";
        }

        if (vino.Orden == OrdenActual)
        {
            clases += " control-hito--actual";
        }

        return clases;
    }

    private string ClaseReloj
    {
        get
        {
            var clases = "control-reloj";

            if (Fase == FaseVotacion.Preparacion)
            {
                clases += " control-reloj--preparacion";
            }
            else if (Fase == FaseVotacion.Pausada)
            {
                clases += " control-reloj--pausa";
            }
            else if (EnAlertaRoja)
            {
                clases += " control-reloj--alerta";
            }

            return clases;
        }
    }

    /// <summary>Acción destructiva a la espera de que el organizador confirme.</summary>
    private sealed record ConfirmacionPendiente(
        string Titulo,
        string Mensaje,
        string TextoConfirmar,
        bool EsPeligro,
        Func<Task> Accion);
}
