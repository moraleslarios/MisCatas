using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;
using MisCatas.Web.Components.Shared;
using MisCatas.Web.Components.Shared.Iconos;
using MisCatas.Web.Seguridad;
using MisCatas.Web.TiempoReal;

namespace MisCatas.Web.Components.Pages.Catas;

/// <summary>
/// Repaso final del participante: una sola pantalla con TODAS sus notas y la
/// posibilidad de corregir cualquiera de ellas antes de que el anfitrión cante
/// el veredicto.
///
/// Reglas que manda el servidor (<c>ServicioVotacion.VotarAsync</c>) y que aquí
/// solo se reflejan en la interfaz:
///  · Mientras la cata está EN CURSO se puede rectificar cualquier vino cuya
///    votación ya esté cerrada, y también el que esté abierto en ese momento.
///  · Una vez FINALIZADA la cata las notas quedan congeladas: esta pantalla
///    pasa a ser solo de lectura, porque las medias y las medallas ya están
///    calculadas.
/// </summary>
public partial class MisNotas : IAsyncDisposable
{
    [Inject]
    private IServicioParticipacion Servicio { get; set; } = default!;

    [Inject]
    private IServicioVotacion ServicioVotacion { get; set; } = default!;

    /// <summary>Conexión en vivo con la mesa, para enterarse de los cambios.</summary>
    [Inject]
    private ClienteCataHub Vivo { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? EstadoAutenticacion { get; set; }

    [Parameter]
    public int Id { get; set; }

    private CataCiegaDto? Datos;
    private bool Cargando = true;
    private bool Guardando;
    private string? Mensaje;
    private string? Error;

    private int? MiUsuarioId;

    /// <summary>Vino que está abierto en el editor; <c>null</c> si no hay ninguno.</summary>
    private int? CataVinoEditando;

    private decimal? NotaEditada;
    private string ComentarioEditado = string.Empty;

    // ---------------------------------------------------------------- estado

    private bool SinConexion => !Vivo.Conectado;

    private bool EnCurso => Datos?.Estado == EstadoCata.EnCurso;

    private bool Finalizada => Datos?.Estado == EstadoCata.Finalizada;

    private int MisVotos => Datos?.Vinos.Count(v => v.MiNota is not null) ?? 0;

    private int MisComentarios =>
        Datos?.Vinos.Count(v => v.MiNota is not null && !string.IsNullOrWhiteSpace(v.MiComentario)) ?? 0;

    private decimal? MiMedia
    {
        get
        {
            var notas = Datos?.Vinos
                .Where(v => v.MiNota is not null)
                .Select(v => v.MiNota!.Value)
                .ToList();

            return notas is { Count: > 0 } ? Math.Round(notas.Average(), 2) : null;
        }
    }

    private string TextoMiMedia => MiMedia is decimal media ? media.ToString("0.##") : "—";

    private string NombreEstado => Datos?.Estado switch
    {
        EstadoCata.Borrador => "En preparación",
        EstadoCata.Abierta => "Lista para empezar",
        EstadoCata.EnCurso => "En directo",
        EstadoCata.Finalizada => "Finalizada",
        _ => "—",
    };

    private string ClasePastilla => Datos?.Estado switch
    {
        EstadoCata.Borrador => "mc-pastilla-borrador",
        EstadoCata.Abierta => "mc-pastilla-abierta",
        EstadoCata.EnCurso => "mc-pastilla-encurso",
        EstadoCata.Finalizada => "mc-pastilla-finalizada",
        _ => "mc-pastilla-borrador",
    };

    // ---------------------------------------------------------------- textos

    private string ClaseExplicacion => EnCurso
        ? "misnotas-explicacion"
        : "misnotas-explicacion misnotas-explicacion--cerrada";

    private Icono IconoExplicacion => EnCurso ? Icono.Lapiz : Icono.Candado;

    private string TextoExplicacion => Datos?.Estado switch
    {
        EstadoCata.EnCurso =>
            "Repasa tus notas con calma: puedes corregir cualquiera de ellas hasta que "
            + "el anfitrión cierre la cata.",
        EstadoCata.Finalizada =>
            "La cata ya está cerrada, así que las notas quedan tal y como las dejaste.",
        _ =>
            "Todavía no hay nada que puntuar: espera a que el anfitrión abra el primer vino.",
    };

    private string TextoEstado(VinoCiegoDto vino) => vino.EstadoVotacion switch
    {
        EstadoVotacion.Abierta => vino.MiNota is not null
            ? "En votación · aún puedes rectificar"
            : "En votación ahora mismo · sin puntuar",
        EstadoVotacion.Cerrada => vino.MiNota is not null
            ? (EnCurso ? "Puntuado · todavía puedes corregirlo" : "Puntuado")
            : (EnCurso ? "Sin puntuar · todavía puedes ponerle nota" : "Te quedaste sin puntuarlo"),
        _ => "Pendiente de abrir",
    };

    private static string TextoNota(VinoCiegoDto vino) =>
        vino.MiNota is decimal nota ? nota.ToString("0.##") : "—";

    private string ClaseFicha(VinoCiegoDto vino)
    {
        var clases = new List<string>(4) { "misnotas-ficha" };

        if (vino.MiNota is not null)
        {
            clases.Add("misnotas-ficha--votado");
        }

        if (vino.EstadoVotacion == EstadoVotacion.Abierta)
        {
            clases.Add("misnotas-ficha--abierta");
        }

        if (EstaEditando(vino))
        {
            clases.Add("misnotas-ficha--editando");
        }

        return string.Join(' ', clases);
    }

    // --------------------------------------------------------------- permisos

    /// <summary>
    /// Se puede tocar un vino solo si la cata sigue viva y ese vino ya ha
    /// estado (o está) en votación. Lo que todavía no se ha lanzado no se
    /// puede puntuar a ciegas.
    /// </summary>
    private bool PuedeEditar(VinoCiegoDto vino) =>
        EnCurso && vino.EstadoVotacion is EstadoVotacion.Abierta or EstadoVotacion.Cerrada;

    private bool EstaEditando(VinoCiegoDto vino) => CataVinoEditando == vino.CataVinoId;

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
    }

    /// <summary>
    /// Aquí NO se salta a ninguna otra pantalla: esta es, a propósito, la
    /// última parada del participante. Si la cata se finaliza mientras repasa,
    /// la pantalla se queda y simplemente deja de permitir cambios.
    /// </summary>
    private async Task AlCambiarEnVivoAsync()
    {
        if (RequiereRelectura(Vivo.UltimoEvento))
        {
            await RecargarAsync();

            // Si el vino que estaba editando deja de ser editable, se cierra el
            // editor para no dejar un formulario que el servidor va a rechazar.
            if (CataVinoEditando is int abierto)
            {
                var vino = Datos?.Vinos.FirstOrDefault(v => v.CataVinoId == abierto);

                if (vino is null || !PuedeEditar(vino))
                {
                    CancelarEdicion();
                }
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private static bool RequiereRelectura(string? evento) => evento is
        EventosCata.VotacionAbierta or
        EventosCata.VotacionCerrada or
        EventosCata.VotoReseteado or
        EventosCata.CataFinalizada or
        EventosCata.CataReabierta;

    // --------------------------------------------------------------- acciones

    private void AbrirEdicion(VinoCiegoDto vino)
    {
        LimpiarAvisos();

        CataVinoEditando = vino.CataVinoId;
        NotaEditada = vino.MiNota;
        ComentarioEditado = vino.MiComentario ?? string.Empty;
    }

    private void CancelarEdicion()
    {
        CataVinoEditando = null;
        NotaEditada = null;
        ComentarioEditado = string.Empty;
    }

    private async Task GuardarAsync()
    {
        LimpiarAvisos();

        if (MiUsuarioId is not int usuarioId
            || CataVinoEditando is not int cataVinoId
            || NotaEditada is not decimal nota
            || Datos?.Vinos.FirstOrDefault(v => v.CataVinoId == cataVinoId) is not { } vino)
        {
            Error = "No se puede guardar esta nota ahora mismo.";
            return;
        }

        Guardando = true;

        try
        {
            var resultado = await ServicioVotacion.VotarAsync(
                Id,
                vino.NumeroOrden,
                usuarioId,
                nota,
                ComentarioEditado);

            if (resultado.Exito)
            {
                Mensaje = $"{vino.Titulo}: nota guardada con un {nota:0.##}.";
                CancelarEdicion();
                await RecargarAsync();
            }
            else
            {
                Error = resultado.Error;
            }
        }
        finally
        {
            Guardando = false;
        }
    }

    private void LimpiarAvisos()
    {
        Mensaje = null;
        Error = null;
    }

    public async ValueTask DisposeAsync()
    {
        Vivo.Cambio -= AlCambiarEnVivoAsync;
        await Vivo.DisposeAsync();
    }
}
