using Microsoft.AspNetCore.Components;

namespace MisCatas.Web.Components.Shared;

/// <summary>Diálogo de confirmación para borrados y otras acciones delicadas.</summary>
public partial class DialogoConfirmar
{
    /// <summary>Indica si el diálogo está visible.</summary>
    [Parameter]
    public bool Visible { get; set; }

    /// <summary>Notifica el cambio de visibilidad al componente padre.</summary>
    [Parameter]
    public EventCallback<bool> VisibleChanged { get; set; }

    [Parameter]
    public string Titulo { get; set; } = "¿Confirmas la acción?";

    [Parameter]
    public string Mensaje { get; set; } = "Esta acción no se puede deshacer.";

    [Parameter]
    public string TextoConfirmar { get; set; } = "Borrar";

    [Parameter]
    public string TextoCancelar { get; set; } = "Cancelar";

    /// <summary>Se ejecuta cuando el usuario confirma.</summary>
    [Parameter]
    public EventCallback AlConfirmar { get; set; }

    private async Task ConfirmarAsync()
    {
        await CerrarAsync();
        await AlConfirmar.InvokeAsync();
    }

    private Task CancelarAsync() => CerrarAsync();

    private async Task CerrarAsync()
    {
        Visible = false;

        if (VisibleChanged.HasDelegate)
        {
            await VisibleChanged.InvokeAsync(false);
        }
    }
}
