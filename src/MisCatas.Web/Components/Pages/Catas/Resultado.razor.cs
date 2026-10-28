using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MisCatas.Application.Participacion;
using MisCatas.Web.Components.Shared;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Pages.Catas;

public partial class Resultado : IDisposable
{
    /// <summary>
    /// Lo que dura la botella del ganador a pantalla completa en el móvil.
    /// Menos que en la televisión: el que mira el teléfono quiere llegar
    /// pronto a su nota, y además puede saltarla con un toque.
    /// </summary>
    private const int SegundosGala = 8;

    /// <summary>Temporizador que retira la gala sola.</summary>
    private CancellationTokenSource? _gala;

    /// <summary>La botella del ganador está tapando la clasificación.</summary>
    private bool _enGala;

    /// <summary>
    /// Vino cuyo detalle de votación se ha pedido desde la rejilla de puestos.
    /// Deja su desplegable abierto y le pone el aro de foco.
    /// </summary>
    private int? _vinoFoco;

    /// <summary>
    /// Vino cuya ficha se está mirando. Se guarda el DTO completo porque la
    /// clasificación ya trae todo lo que la ficha necesita: no hay que volver
    /// a preguntar a la base de datos.
    /// </summary>
    private VinoReveladoDto? _vinoDetalle;

    /// <summary>La ficha del vino está abierta encima de la clasificación.</summary>
    private bool _detalleVisible;

    [Inject]
    private IServicioParticipacion Servicio { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? EstadoAutenticacion { get; set; }

    [Parameter]
    public int Id { get; set; }

    private ResultadoCataDto? Datos { get; set; }

    private bool Cargando { get; set; } = true;

    private string TextoFecha => Datos is null
        ? ""
        : Datos.Fecha.ToString("dddd d 'de' MMMM 'de' yyyy");

    /// <summary>
    /// Se celebra al ganador a pantalla completa, como en el escaparate, pero
    /// solo mientras dure la gala y solo si de verdad hay un ganador.
    /// </summary>
    private bool EnGala => _enGala && Datos?.Ganador is not null;

    /// <summary>Cuántos vinos voté yo de los que había en la mesa.</summary>
    private int MisVotos => Datos?.Clasificacion.Count(v => v.MiNota is not null) ?? 0;

    /// <summary>Media de mis propias notas en toda la cata.</summary>
    private string TextoMiMedia
    {
        get
        {
            var notas = Datos?.Clasificacion
                .Where(v => v.MiNota is not null)
                .Select(v => v.MiNota!.Value)
                .ToList();

            return notas is null || notas.Count == 0
                ? "—"
                : Math.Round(notas.Average(), 2).ToString("0.##");
        }
    }

    /// <summary>Media de las medias: el nivel general de la cata.</summary>
    private string TextoMediaCata
    {
        get
        {
            var medias = Datos?.Clasificacion
                .Where(v => v.NotaMedia is not null)
                .Select(v => v.NotaMedia!.Value)
                .ToList();

            return medias is null || medias.Count == 0
                ? "—"
                : Math.Round(medias.Average(), 2).ToString("0.0");
        }
    }

    /// <summary>Total de comentarios escritos por todo el mundo.</summary>
    private int TotalComentarios => Datos?.Clasificacion.Sum(v => v.Comentaristas) ?? 0;

    /// <summary>Total de votos contados en la cata entera.</summary>
    private int TotalVotos => Datos?.Clasificacion.Sum(v => v.VotosContados) ?? 0;

    /// <summary>La mejor relación calidad-precio, por si hubo sorpresa.</summary>
    private VinoReveladoDto? Chollo => Datos?.Clasificacion
        .Where(v => v.PrecioEstimado is > 0 && v.NotaMedia is not null)
        .OrderByDescending(v => v.NotaMedia!.Value / v.PrecioEstimado!.Value)
        .FirstOrDefault();

    /// <summary>El farolillo rojo, solo si hay más de un vino.</summary>
    private VinoReveladoDto? Colista => Datos is null || Datos.Clasificacion.Count < 2
        ? null
        : Datos.Clasificacion[^1];

    /// <summary>
    /// Vitrina de toda la cata ya destapada: aquí sí se pasa la etiqueta real,
    /// el puesto (que pinta el medallón oro/plata/bronce) y la nota media como
    /// sello. El podio va destacado a doble ancho.
    /// </summary>
    private IReadOnlyList<PiezaVitrina> PiezasVitrina => Datos is null
        ? []
        : [.. Datos.Clasificacion.Select(vino => new PiezaVitrina(
            Titulo: vino.NombreConAnio,
            Subtitulo: string.IsNullOrWhiteSpace(vino.Bodega) ? vino.Dueno : vino.Bodega,
            Nota: vino.NotaMedia?.ToString("0.0"),
            Detalle: $"Lo trajo {vino.Dueno}",
            NumeroOrden: vino.NumeroOrden,
            UrlEtiqueta: UrlEtiqueta(vino),
            Puesto: vino.Posicion,
            // Sin enlace: la pieza abre la ficha con AlPulsarPieza. Un ancla
            // hacía navegar y sacaba al catador de esta pantalla.
            Enlace: null,
            Destacada: vino.Posicion == 1))];

    protected override async Task OnParametersSetAsync()
    {
        Cargando = true;

        int? usuarioId = null;

        if (EstadoAutenticacion is not null)
        {
            var estado = await EstadoAutenticacion;
            usuarioId = estado.User.ObtenerUsuarioId();
        }

        Datos = await Servicio.ObtenerResultadoAsync(Id, usuarioId);

        Cargando = false;

        // La gala solo se monta al abrir la pantalla; si se vuelve a entrar
        // desde la lista de catas se repite, que es justo lo que se espera.
        if (Datos?.Ganador is not null)
        {
            ArrancarGala();
        }
    }

    /// <summary>
    /// Enciende la botella del ganador y programa su retirada. La espera va en
    /// segundo plano: la pantalla no puede quedarse bloqueada ocho segundos.
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
                // Lo han saltado a mano: no hay nada que retirar.
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

    /// <summary>Saltar la celebración de un toque e ir a la clasificación.</summary>
    private void CerrarGala() => PararGala();

    /// <summary>
    /// Se ha pulsado un puesto de la rejilla: se abre la ficha del vino encima
    /// de la pantalla. No se navega a ningún sitio, así nadie pierde el punto
    /// por el que iba leyendo la clasificación.
    /// </summary>
    private void AbrirDetalle(VinoReveladoDto vino)
    {
        PararGala();
        _vinoFoco = vino.VinoId;
        _vinoDetalle = vino;
        _detalleVisible = true;
    }

    /// <summary>
    /// Igual que <see cref="AbrirDetalle"/>, pero desde la estantería. La pieza
    /// solo lleva el número de orden, que dentro de una cata es único, así que
    /// basta para recuperar el vino completo.
    /// </summary>
    private void AbrirDesdeVitrina(PiezaVitrina pieza)
    {
        var vino = Datos?.Clasificacion
            .FirstOrDefault(v => v.NumeroOrden == pieza.NumeroOrden);

        if (vino is not null)
        {
            AbrirDetalle(vino);
        }
    }

    /// <summary>Clase del botón de cada puesto en la rejilla de resumen.</summary>
    private string ClaseRejilla(VinoReveladoDto vino)
    {
        var clases = "resultado-rejilla-item";

        if (vino.Posicion <= 3)
        {
            clases += $" resultado-rejilla-item--puesto{vino.Posicion}";
        }

        if (_vinoFoco == vino.VinoId)
        {
            clases += " resultado-rejilla-item--foco";
        }

        return clases;
    }

    private static string ClaseFicha(VinoReveladoDto vino)
    {
        var clases = "resultado-ficha";

        if (vino.Posicion <= 3)
        {
            clases += $" resultado-ficha--puesto{vino.Posicion}";
        }

        return clases;
    }

    private static string ClasePuesto(VinoReveladoDto vino) => vino.Posicion switch
    {
        1 => "resultado-puesto resultado-puesto--oro",
        2 => "resultado-puesto resultado-puesto--plata",
        3 => "resultado-puesto resultado-puesto--bronce",
        _ => "resultado-puesto"
    };

    private static string TextoMedia(VinoReveladoDto vino) =>
        vino.NotaMedia is null ? "—" : vino.NotaMedia.Value.ToString("0.0");

    private static string TextoMiNota(VinoReveladoDto vino) =>
        vino.MiNota is null ? "No voté" : vino.MiNota.Value.ToString("0.0");

    private static string TextoNota(decimal? nota) =>
        nota is null ? "—" : nota.Value.ToString("0.0");

    /// <summary>
    /// Imagen que mejor representa al vino: la etiqueta si la hay, y si no la
    /// foto de la botella. Si no hay ninguna, se dibuja la botella virtual.
    /// </summary>
    private static string? UrlEtiqueta(VinoReveladoDto vino) => vino switch
    {
        { TieneEtiqueta: true } => $"/imagenes/vinos/{vino.VinoId}/etiqueta",
        { TieneFoto: true } => $"/imagenes/vinos/{vino.VinoId}/foto",
        _ => null
    };

    public void Dispose() => PararGala();
}
