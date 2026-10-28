using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MisCatas.Application.Vinos;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Hoja superpuesta con la ficha de una botella, pensada para el organizador.
///
/// A diferencia de <see cref="ModalVino"/>, que destapa un vino ya puntuado y
/// enseña los votos, esta solo muestra los datos de la botella: sirve para que
/// el organizador reconozca qué vino va a lanzar antes de lanzarlo.
///
/// No consulta nada: recibe el <see cref="VinoDto"/> ya cargado por la
/// pantalla que la abre.
/// </summary>
public partial class ModalFichaVino
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
    /// Botella a mostrar. Si es <c>null</c> el componente no pinta nada,
    /// aunque <see cref="Visible"/> sea cierto.
    /// </summary>
    [Parameter]
    public VinoDto? Vino { get; set; }

    /// <summary>
    /// Posición del vino en el recorrido de la cata, si se abre desde una.
    /// Si es <c>null</c> no se muestra ningún número.
    /// </summary>
    [Parameter]
    public int? NumeroOrden { get; set; }

    /// <summary>
    /// Botones extra para el pie de la hoja, por ejemplo uno para lanzar
    /// directamente este vino.
    /// </summary>
    [Parameter]
    public RenderFragment? Acciones { get; set; }

    /// <summary>Se ejecuta al cerrar, por el aspa, el fondo o Escape.</summary>
    [Parameter]
    public EventCallback AlCerrar { get; set; }

    /// <summary>Etiqueta real del vino, si se subió.</summary>
    private string? UrlEtiqueta => Vino is { TieneEtiqueta: true } vino
        ? $"/imagenes/vinos/{vino.Id}/etiqueta"
        : null;

    /// <summary>Foto de la botella, si se subió.</summary>
    private string? UrlFoto => Vino is { TieneFoto: true } vino
        ? $"/imagenes/vinos/{vino.Id}/foto"
        : null;

    protected override async Task OnAfterRenderAsync(bool primeraVez)
    {
        // Al abrirse, el foco va al aspa: la hoja se cierra solo con teclado y
        // el lector de pantalla anuncia el diálogo.
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
}
