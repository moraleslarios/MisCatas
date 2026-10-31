using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;
using MisCatas.Web.Components.Shared;
using MisCatas.Web.Seguridad;
using MisCatas.Web.TiempoReal;

namespace MisCatas.Web.Components.Pages.Catas;

/// <summary>
/// Resumen de la cata para el participante. Se refresca en vivo: el progreso
/// llega ya calculado desde el servidor por <see cref="ClienteCataHub"/>.
/// </summary>
public partial class Resumen : IAsyncDisposable
{
    [Inject]
    private IServicioParticipacion Servicio { get; set; } = default!;

    /// <summary>Conexión en vivo con la mesa (una por circuito de Blazor).</summary>
    [Inject]
    private ClienteCataHub Vivo { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? EstadoAutenticacion { get; set; }

    [Parameter]
    public int Id { get; set; }

    private CataCiegaDto? Datos { get; set; }

    private bool Cargando { get; set; } = true;

    private int? MiUsuarioId;

    /// <summary>
    /// Progreso en directo si el hub ya lo ha mandado; si no, el de la carga.
    /// </summary>
    private ProgresoCataDto Progreso =>
        Vivo.Progreso ?? Datos?.Progreso ?? new ProgresoCataDto(0, 0, 0, null, 0, 0, null, null);

    private bool SinConexion => !Vivo.Conectado;

    private string TextoFecha => Datos is null
        ? ""
        : Datos.Fecha.ToString("dddd d 'de' MMMM 'de' yyyy");

    /// <summary>Cuántos vinos he votado yo ya.</summary>
    private int MisVotos => Datos?.Vinos.Count(v => v.HeVotado) ?? 0;

    /// <summary>Cuántos vinos he dejado pasar sin votar (ya cerrados).</summary>
    private int MisVotosPerdidos => Datos?.Vinos
        .Count(v => !v.HeVotado && v.EstadoVotacion == EstadoVotacion.Cerrada) ?? 0;

    /// <summary>Media de mis propias notas, para compararme conmigo mismo.</summary>
    private decimal? MiMedia
    {
        get
        {
            var notas = Datos?.Vinos
                .Where(v => v.MiNota is not null)
                .Select(v => v.MiNota!.Value)
                .ToList();

            return notas is null || notas.Count == 0 ? null : Math.Round(notas.Average(), 2);
        }
    }

    private string TextoMiMedia => MiMedia is null ? "—" : MiMedia.Value.ToString("0.##");

    /// <summary>Cuántos comentarios he escrito.</summary>
    private int MisComentarios => Datos?.Vinos
        .Count(v => !string.IsNullOrWhiteSpace(v.MiComentario)) ?? 0;

    private string NombreEstado => Datos?.Estado switch
    {
        EstadoCata.Borrador => "En preparación",
        EstadoCata.Abierta => "Lista para empezar",
        EstadoCata.EnCurso => "En directo",
        EstadoCata.Finalizada => "Finalizada",
        _ => ""
    };

    private string ClasePastilla => Datos?.Estado switch
    {
        EstadoCata.Borrador => "resumen-pastilla resumen-pastilla--borrador",
        EstadoCata.Abierta => "resumen-pastilla resumen-pastilla--abierta",
        EstadoCata.EnCurso => "resumen-pastilla resumen-pastilla--encurso",
        EstadoCata.Finalizada => "resumen-pastilla resumen-pastilla--finalizada",
        _ => "resumen-pastilla"
    };

    private bool Finalizada => Datos?.Estado == EstadoCata.Finalizada;

    /// <summary>La cata sigue viva: todavía se pueden corregir notas.</summary>
    private bool EnCurso => Datos?.Estado == EstadoCata.EnCurso;

    /// <summary>
    /// Vitrina de la cata vista desde el móvil. REGLA DE ORO: aquí la cata
    /// sigue a ciegas, así que nunca se pasa <c>UrlEtiqueta</c>; la botella se
    /// pinta con su número de orden y, como sello, MI nota (no la media del
    /// grupo, que no se destapa hasta el final).
    /// </summary>
    private IReadOnlyList<PiezaVitrina> PiezasVitrina => Datos is null
        ? []
        : [.. Datos.Vinos.Select(vino => new PiezaVitrina(
            Titulo: vino.Titulo,
            Subtitulo: TextoEstado(vino),
            Nota: vino.MiNota?.ToString("0.##"),
            Detalle: string.IsNullOrWhiteSpace(vino.MiComentario) ? null : "Con nota escrita",
            NumeroOrden: vino.NumeroOrden,
            UrlEtiqueta: null,
            Puesto: null,
            Enlace: null,
            Destacada: vino.EstadoVotacion == EstadoVotacion.Abierta))];

    protected override async Task OnInitializedAsync()
    {
        if (EstadoAutenticacion is not null)
        {
            var estado = await EstadoAutenticacion;
            MiUsuarioId = estado.User.ObtenerUsuarioId();
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
    }

    private async Task AlCambiarEnVivoAsync()
    {
        if (RequiereRelectura(Vivo.UltimoEvento))
        {
            await RecargarAsync();
        }

        // Se ha cantado el veredicto: todo el mundo a la pantalla de
        // resultados, que es donde se destapan los vinos. Nadie tiene que
        // tocar nada en el móvil.
        if (Vivo.UltimoEvento is EventosCata.CataFinalizada)
        {
            await InvokeAsync(() => Navegacion.NavigateTo(RutasApp.Resultado(Id)));
            return;
        }

        // El anfitrión acaba de arrancar un vino: a quien está esperando en el
        // resumen se le lleva derecho a la papeleta, que es lo que espera ver.
        if (Vivo.UltimoEvento is EventosCata.CuentaAtrasIniciada or EventosCata.VotacionAbierta)
        {
            await InvokeAsync(() => Navegacion.NavigateTo(RutasApp.Votar(Id)));
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Los ticks del reloj y los resúmenes no tocan la lista de votos; solo los
    /// cambios de fase obligan a releer.
    /// </summary>
    private static bool RequiereRelectura(string? evento) => evento is
        EventosCata.VotacionAbierta or
        EventosCata.VotacionCerrada or
        EventosCata.VotoReseteado or
        EventosCata.CataFinalizada or
        EventosCata.CataReabierta;

    private static string ClaseFila(VinoCiegoDto vino)
    {
        var clases = "resumen-fila";

        if (vino.HeVotado)
        {
            clases += " resumen-fila--votado";
        }

        if (vino.EstadoVotacion == EstadoVotacion.Abierta)
        {
            clases += " resumen-fila--abierta";
        }

        return clases;
    }

    private static string TextoEstado(VinoCiegoDto vino) => vino.EstadoVotacion switch
    {
        EstadoVotacion.Abierta => vino.HeVotado ? "Votado · aún puedes rectificar" : "Votación abierta",
        EstadoVotacion.Cerrada => vino.HeVotado ? "Votado" : "No llegaste a votar",
        _ => "Pendiente",
    };

    private static string TextoNota(VinoCiegoDto vino) =>
        vino.MiNota is null ? "—" : vino.MiNota.Value.ToString("0.##");

    public async ValueTask DisposeAsync()
    {
        Vivo.Cambio -= AlCambiarEnVivoAsync;
        await Vivo.DisposeAsync();
    }
}
