using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MisCatas.Web.Components.Shared.Iconos;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Modal de confirmación de la identidad visual de MisCatas. Sustituye a
/// <c>window.confirm</c> y se usa tanto para borrados como para el aviso de
/// salir de una cata activa.
/// </summary>
public partial class ModalConfirmacion
{
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    private ElementReference _caja;
    private ElementReference _botonConfirmar;
    private bool _visibleAnterior;

    /// <summary>Si el modal está en pantalla. Admite <c>@bind-Visible</c>.</summary>
    [Parameter]
    public bool Visible { get; set; }

    /// <summary>Notifica el cambio de <see cref="Visible"/>.</summary>
    [Parameter]
    public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>Encabezado del modal.</summary>
    [Parameter]
    public string Titulo { get; set; } = "¿Confirmas la acción?";

    /// <summary>Explicación de lo que va a ocurrir.</summary>
    [Parameter]
    public string Mensaje { get; set; } = "";

    /// <summary>Contenido extra bajo el mensaje. Opcional.</summary>
    [Parameter]
    public RenderFragment? Detalle { get; set; }

    /// <summary>Texto del botón que confirma.</summary>
    [Parameter]
    public string TextoConfirmar { get; set; } = "Confirmar";

    /// <summary>Texto del botón que cancela.</summary>
    [Parameter]
    public string TextoCancelar { get; set; } = "Cancelar";

    /// <summary>
    /// Pinta el botón de confirmar en rojo. Para acciones destructivas.
    /// </summary>
    [Parameter]
    public bool EsPeligro { get; set; }

    /// <summary>Icono de la cabecera. Si no se indica, se elige por contexto.</summary>
    [Parameter]
    public Icono? Icono { get; set; }

    /// <summary>Se ejecuta al confirmar.</summary>
    [Parameter]
    public EventCallback AlConfirmar { get; set; }

    /// <summary>Se ejecuta al cancelar o cerrar con Escape.</summary>
    [Parameter]
    public EventCallback AlCancelar { get; set; }

    private Icono IconoCabecera =>
        Icono ?? (EsPeligro ? Iconos.Icono.Alerta : Iconos.Icono.Hecho);

    private string ClaseConfirmar => EsPeligro
        ? "mc-boton mc-boton-peligro modal-boton"
        : "mc-boton mc-boton-primario modal-boton";

    protected override async Task OnAfterRenderAsync(bool primeraVez)
    {
        // Al abrirse, el foco va al botón de confirmar: así el modal es usable
        // solo con teclado y el lector de pantalla anuncia el diálogo.
        if (Visible && !_visibleAnterior)
        {
            _visibleAnterior = true;
            await _botonConfirmar.FocusAsync();
        }
        else if (!Visible && _visibleAnterior)
        {
            _visibleAnterior = false;
        }
    }

    private async Task AlPulsarTeclaAsync(KeyboardEventArgs argumentos)
    {
        if (argumentos.Key is "Escape" or "Esc")
        {
            await CancelarAsync();
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
    }

    private async Task ConfirmarAsync()
    {
        await CerrarAsync();

        if (AlConfirmar.HasDelegate)
        {
            await AlConfirmar.InvokeAsync();
        }
    }

    private async Task CancelarAsync()
    {
        await CerrarAsync();

        if (AlCancelar.HasDelegate)
        {
            await AlCancelar.InvokeAsync();
        }
    }
}
