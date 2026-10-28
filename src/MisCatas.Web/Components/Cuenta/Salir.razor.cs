using Microsoft.AspNetCore.Components;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Cuenta;

public partial class Salir
{
    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    [CascadingParameter]
    private HttpContext ContextoHttp { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await SesionCookie.SalirAsync(ContextoHttp);

        Navegacion.NavigateTo(RutasCuenta.Login, forceLoad: true);
    }
}
