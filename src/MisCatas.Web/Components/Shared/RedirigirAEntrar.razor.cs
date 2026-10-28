using Microsoft.AspNetCore.Components;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Shared;

/// <summary>Redirige al formulario de acceso conservando la ruta de destino.</summary>
public partial class RedirigirAEntrar
{
    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    protected override void OnInitialized()
    {
        var destino = Navegacion.ToBaseRelativePath(Navegacion.Uri);

        var url = Navegacion.GetUriWithQueryParameters(
            RutasCuenta.Login,
            new Dictionary<string, object?> { ["volverA"] = $"/{destino}" });

        Navegacion.NavigateTo(url, forceLoad: true);
    }
}
