using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using MisCatas.Application.Autenticacion;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Cuenta;

public partial class Entrar
{
    [Inject]
    private IServicioAutenticacion ServicioAutenticacion { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    [CascadingParameter]
    private HttpContext ContextoHttp { get; set; } = default!;

    private DatosEntrar _datos = new();

    /// <summary>
    /// Datos del formulario. Blazor asigna <c>null</c> cuando el POST
    /// pertenece a otro formulario de la página, así que el <c>set</c> nunca
    /// deja la propiedad nula. No lleva inicializador de propiedad para no
    /// disparar la advertencia BL0008.
    /// </summary>
    [SupplyParameterFromForm]
    private DatosEntrar Datos
    {
        get => _datos;
        set => _datos = value ?? new DatosEntrar();
    }

    [SupplyParameterFromQuery(Name = "volverA")]
    private string? VolverA { get; set; }

    private string? Error { get; set; }

    protected override async Task OnInitializedAsync()
    {
        // Si ya hay cookie, la limpiamos antes de pintar el formulario para
        // evitar quedarse con una sesión a medias.
        if (HttpMethods.IsGet(ContextoHttp.Request.Method)
            && ContextoHttp.User.Identity?.IsAuthenticated == true)
        {
            await SesionCookie.SalirAsync(ContextoHttp);
        }
    }

    private async Task EntrarAsync()
    {
        Error = null;

        var resultado = await ServicioAutenticacion.ValidarCredencialesAsync(
            Datos.Login,
            Datos.Contrasena);

        if (resultado.EsFallo || resultado.Valor is null)
        {
            Error = resultado.Error ?? "No se ha podido iniciar sesión.";
            return;
        }

        var identidad = resultado.Valor;

        await SesionCookie.EntrarAsync(ContextoHttp, identidad, Datos.Recordarme);

        if (identidad.DebeCambiarPassword)
        {
            Navegacion.NavigateTo(RutasCuenta.CambiarPassword, forceLoad: true);
            return;
        }

        // Sin destino explícito, cada rol entra en su propia pantalla de inicio.
        var destino = string.IsNullOrWhiteSpace(VolverA)
            ? RutasApp.InicioSegunRol(identidad.Rol)
            : VolverA;

        Navegacion.NavigateTo(destino, forceLoad: true);
    }

    /// <summary>Datos del formulario de acceso.</summary>
    private sealed class DatosEntrar
    {
        [Required(ErrorMessage = "Escribe tu usuario.")]
        public string Login { get; set; } = string.Empty;

        [Required(ErrorMessage = "Escribe tu contraseña.")]
        public string Contrasena { get; set; } = string.Empty;

        public bool Recordarme { get; set; } = true;
    }
}
