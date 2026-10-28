using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MisCatas.Application.Participacion;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Hoja superpuesta con la ficha completa de un vino ya destapado: sus datos
/// de botella y, si procede, lo que ocurrió con él en la cata.
///
/// No pide nada al servidor: recibe el <see cref="VinoReveladoDto"/> que la
/// pantalla de resultado ya tiene cargado. Por eso abre al instante y por eso
/// no necesita ruta propia.
/// </summary>
public partial class ModalVino
{
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    private ElementReference _botonCerrar;
    private bool _visibleAnterior;

    /// <summary>Si la ficha está en pantalla. Admite <c>@bind-Visible</c>.</summary>
    [Parameter]
    public bool Visible { get; set; }

    /// <summary>Notifica el cambio de <see cref="Visible"/>.</summary>
    [Parameter]
    public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>
    /// Vino a mostrar. Si es <c>null</c> el componente no pinta nada, aunque
    /// <see cref="Visible"/> sea cierto.
    /// </summary>
    [Parameter]
    public VinoReveladoDto? Vino { get; set; }

    /// <summary>
    /// Si se enseña el bloque de puesto, notas y votos. Se pone a <c>false</c>
    /// cuando la ficha se abre fuera de una cata ya destapada.
    /// </summary>
    [Parameter]
    public bool MostrarResultados { get; set; } = true;

    /// <summary>Se ejecuta al cerrar, por el aspa, el fondo o Escape.</summary>
    [Parameter]
    public EventCallback AlCerrar { get; set; }

    /// <summary>Etiqueta real del vino, si se subió.</summary>
    private string? UrlEtiqueta => Vino is { TieneEtiqueta: true } vino
        ? $"/imagenes/vinos/{vino.VinoId}/etiqueta"
        : null;

    /// <summary>Foto de la botella, si se subió.</summary>
    private string? UrlFoto => Vino is { TieneFoto: true } vino
        ? $"/imagenes/vinos/{vino.VinoId}/foto"
        : null;

    protected override async Task OnAfterRenderAsync(bool primeraVez)
    {
        // Al abrirse, el foco va al aspa: así la hoja se puede cerrar solo con
        // teclado y el lector de pantalla anuncia el diálogo.
        if (Visible && Vino is not null && !_visibleAnterior)
        {
            _visibleAnterior = true;
            await _botonCerrar.FocusAsync();
        }
        else if ((!Visible || Vino is null) && _visibleAnterior)
        {
            _visibleAnterior = false;
        }
    }

    private async Task AlPulsarTeclaAsync(KeyboardEventArgs argumentos)
    {
        if (argumentos.Key is "Escape" or "Esc")
        {
            await CerrarAsync();
        }
    }

    private async Task CerrarAsync()
    {
        if (VisibleChanged.HasDelegate)
        {
            await VisibleChanged.InvokeAsync(false);
        }
        else
        {
            Visible = false;
        }

        if (AlCerrar.HasDelegate)
        {
            await AlCerrar.InvokeAsync();
        }
    }

    /// <summary>Medallón del puesto, con el color del podio.</summary>
    private static string ClasePuesto(VinoReveladoDto vino) => vino.Posicion switch
    {
        1 => "ficha-puesto ficha-puesto--oro",
        2 => "ficha-puesto ficha-puesto--plata",
        3 => "ficha-puesto ficha-puesto--bronce",
        _ => "ficha-puesto",
    };

    /// <summary>Nota suelta en texto, o un guion si no hay.</summary>
    private static string TextoNota(decimal? nota) =>
        nota is null ? "—" : nota.Value.ToString("0.0");
}
