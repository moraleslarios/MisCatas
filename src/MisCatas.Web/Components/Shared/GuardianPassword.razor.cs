using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Si el usuario tiene pendiente el cambio de contraseña inicial, lo redirige
/// a la pantalla correspondiente y no le deja usar el resto de la aplicación.
/// </summary>
public partial class GuardianPassword
{
    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? EstadoAutenticacion { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (EstadoAutenticacion is null)
        {
            return;
        }

        var estado = await EstadoAutenticacion;

        if (!estado.User.DebeCambiarPassword())
        {
            return;
        }

        var rutaActual = Navegacion.ToBaseRelativePath(Navegacion.Uri);

        // Evita el bucle: la propia pantalla de cambio no se redirige.
        if (rutaActual.StartsWith("cuenta/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Navegacion.NavigateTo(RutasCuenta.CambiarPassword, forceLoad: true);
    }
}
