using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;
using MisCatas.Web.Seguridad;
using MisCatas.Web.TiempoReal;

namespace MisCatas.Web.Components.Pages.Catas;

/// <summary>
/// Pantalla de votación del participante. Todo el tiempo y toda la fase los
/// manda el servidor a través de <see cref="ClienteCataHub"/>: aquí NO hay
/// ningún reloj propio, para que los diez móviles vean exactamente lo mismo.
/// </summary>
public partial class Votar : IAsyncDisposable
{
    [Inject]
    private IServicioParticipacion Servicio { get; set; } = default!;

    [Inject]
    private IServicioVotacion ServicioVotacion { get; set; } = default!;

    /// <summary>Conexión en vivo con la mesa (una por circuito de Blazor).</summary>
    [Inject]
    private ClienteCataHub Vivo { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? EstadoAutenticacion { get; set; }

    [Parameter]
    public int Id { get; set; }

    private CataCiegaDto? Datos;
    private bool Cargando = true;
    private bool Enviando;
    private bool ConfirmacionVisible;
    private string? Mensaje;
    private string? Error;

    private decimal? Nota;
    private string Comentario = string.Empty;

    private int? MiUsuarioId;

    /// <summary>Vino cuyo formulario está cargado ahora mismo en pantalla.</summary>
    private int? _ordenCargado;

    // ---------------------------------------------------------------- estado

    private EstadoVivoDto? Estado => Vivo.Estado;

    private FaseVotacion Fase => Estado?.Fase ?? FaseVotacion.Inactiva;

    /// <summary>Número de vino que manda el servidor; si no hay señal, el de la carga.</summary>
    private int? OrdenActual => Estado?.NumeroVinoActual ?? Datos?.NumeroVinoActual;

    private VinoCiegoDto? VinoActual =>
        OrdenActual is int orden && Datos is not null
            ? Datos.Vinos.FirstOrDefault(v => v.NumeroOrden == orden)
            : Datos?.VinoEnVotacion;

    /// <summary>Cuenta atrás previa de 5 a 0 antes de abrir el turno.</summary>
    private bool EnPreparacion => Fase == FaseVotacion.Preparacion;

    /// <summary>El administrador ha congelado el reloj: ni corre el tiempo ni se admiten votos.</summary>
    private bool EnPausa => Fase == FaseVotacion.Pausada;

    private bool VotacionAbierta => Estado is { } vivo
        ? vivo.AceptaVotos
        : Datos?.EstadoVinoActual == EstadoVotacion.Abierta;

    /// <summary>Entre vinos el participante todavía puede corregir su última nota.</summary>
    private bool PuedeRectificar => Estado?.PermiteRectificar == true && VinoActual?.HeVotado == true;

    private bool PuedeVotar => (VotacionAbierta || PuedeRectificar) && !Enviando && VinoActual is not null;

    private bool PuedeEnviar => PuedeVotar && Nota is not null;

    private int SegundosRestantes => Vivo.SegundosRestantesEnteros;

    /// <summary>
    /// Cuenta atrás de preparación (5 → 0). Viaja aparte del tiempo de
    /// votación, que durante esta fase todavía no corre.
    /// </summary>
    private int SegundosPreparacion => Vivo.SegundosPreparacionEnteros;

    private int SegundosTotales => Estado?.SegundosTotales ?? Datos?.TiempoVotoSegundos ?? 90;

    private int PorcentajeAlerta => Estado?.PorcentajeAlertaRoja ?? 25;

    private bool EnAlertaRoja => Vivo.EnAlertaRoja;

    private bool SinConexion => !Vivo.Conectado;

    // ---------------------------------------------------------------- textos

    private string TituloVino => VinoActual?.Titulo ?? "Sin vino en votación";

    private string TextoBotonEnviar => VinoActual?.HeVotado == true
        ? "Rectificar mi nota"
        : "Enviar mi nota";

    private string MensajeConfirmacion =>
        $"Vas a puntuar el {TituloVino.ToLowerInvariant()} con un {Nota:0.##}. " +
        "Podrás rectificar mientras siga abierta la votación.";

    private string TextoEstadoVotacion => Fase switch
    {
        FaseVotacion.Preparacion => "Preparados…",
        FaseVotacion.Abierta => "Votación abierta",
        FaseVotacion.Pausada => "Votación en pausa",
        FaseVotacion.Cerrada => "Votación cerrada",
        _ => Datos?.EstadoVinoActual switch
        {
            EstadoVotacion.Abierta => "Votación abierta",
            EstadoVotacion.Cerrada => "Votación cerrada",
            _ => "En espera",
        },
    };

    private string ClasePastillaEstado => Fase switch
    {
        FaseVotacion.Abierta => "mc-pastilla-encurso",
        FaseVotacion.Cerrada => "mc-pastilla-finalizada",
        FaseVotacion.Preparacion => "mc-pastilla-abierta",
        FaseVotacion.Pausada => "mc-pastilla-borrador",
        _ => Datos?.EstadoVinoActual switch
        {
            EstadoVotacion.Abierta => "mc-pastilla-encurso",
            EstadoVotacion.Cerrada => "mc-pastilla-finalizada",
            _ => "mc-pastilla-borrador",
        },
    };

    // ------------------------------------------------------------ ciclo vida

    protected override async Task OnInitializedAsync()
    {
        if (EstadoAutenticacion is not null)
        {
            var autenticacion = await EstadoAutenticacion;
            MiUsuarioId = autenticacion.User.ObtenerUsuarioId();
        }

        Vivo.Cambio += AlCambiarEnVivoAsync;
    }

    protected override async Task OnParametersSetAsync()
    {
        Cargando = true;
        await RecargarAsync();
        Cargando = false;

        await Vivo.ConectarAsync(Id);
    }

    private async Task RecargarAsync()
    {
        if (MiUsuarioId is not int usuarioId)
        {
            Datos = null;
            return;
        }

        Datos = await Servicio.ObtenerCataCiegaAsync(Id, usuarioId);
        RellenarFormulario(forzar: false);
    }

    /// <summary>
    /// Deja el formulario con la nota y el comentario ya emitidos. Solo lo
    /// reescribe cuando cambia el vino, para no pisar lo que el usuario está
    /// tecleando justo cuando llega un evento del servidor.
    /// </summary>
    private void RellenarFormulario(bool forzar)
    {
        var orden = VinoActual?.NumeroOrden;

        if (!forzar && orden == _ordenCargado)
        {
            return;
        }

        _ordenCargado = orden;
        Nota = VinoActual?.MiNota;
        Comentario = VinoActual?.MiComentario ?? string.Empty;
    }

    private async Task AlCambiarEnVivoAsync()
    {
        if (RequiereRelectura(Vivo.UltimoEvento))
        {
            await RecargarAsync();
        }

        // Fin de la cata: el móvil salta solo a la pantalla de resultados, en
        // paralelo con la gala del escaparate. Quien está votando no tiene que
        // buscar nada ni volver atrás a mano.
        if (Vivo.UltimoEvento is EventosCata.CataFinalizada)
        {
            await InvokeAsync(() => Navegacion.NavigateTo(RutasApp.Resultado(Id)));
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Los ticks de reloj no tocan la base de datos; solo los cambios de fase
    /// obligan a releer los votos.
    /// </summary>
    private static bool RequiereRelectura(string? evento) => evento is
        EventosCata.VotacionAbierta or
        EventosCata.VotacionCerrada or
        EventosCata.VotoReseteado or
        EventosCata.CataFinalizada or
        EventosCata.CataReabierta or
        EventosCata.CuentaAtrasIniciada;

    // --------------------------------------------------------------- acciones

    private Task PedirConfirmacionAsync()
    {
        LimpiarAvisos();

        if (!PuedeEnviar)
        {
            Error = "No se puede votar ahora mismo.";
            return Task.CompletedTask;
        }

        ConfirmacionVisible = true;
        return Task.CompletedTask;
    }

    private async Task VotarAsync()
    {
        LimpiarAvisos();

        if (MiUsuarioId is not int usuarioId || VinoActual is not { } vino || Nota is not decimal nota)
        {
            Error = "No se puede votar ahora mismo.";
            return;
        }

        Enviando = true;

        try
        {
            var resultado = await ServicioVotacion.VotarAsync(Id, vino.NumeroOrden, usuarioId, nota, Comentario);

            if (resultado.Exito)
            {
                var emitido = resultado.Valor;
                Mensaje = emitido?.FueRectificado == true
                    ? $"Nota rectificada: {emitido.Nota:0.#}."
                    : $"Nota registrada: {nota:0.#}.";

                await RecargarAsync();
                RellenarFormulario(forzar: true);
            }
            else
            {
                Error = resultado.Error;
            }
        }
        finally
        {
            Enviando = false;
        }
    }

    private void LimpiarAvisos()
    {
        Mensaje = null;
        Error = null;
    }

    private string ClaseHito(VinoCiegoDto vino)
    {
        var clases = new List<string>(3) { "votar-hito" };

        if (vino.HeVotado)
        {
            clases.Add("votar-hito--hecho");
        }

        if (VinoActual is { } actual && actual.CataVinoId == vino.CataVinoId)
        {
            clases.Add("votar-hito--actual");
        }

        return string.Join(' ', clases);
    }

    public async ValueTask DisposeAsync()
    {
        Vivo.Cambio -= AlCambiarEnVivoAsync;
        await Vivo.DisposeAsync();
    }
}
