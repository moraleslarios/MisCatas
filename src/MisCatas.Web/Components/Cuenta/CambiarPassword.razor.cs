using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using MisCatas.Application.Autenticacion;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Cuenta;

public partial class CambiarPassword
{
    [Inject]
    private IServicioAutenticacion ServicioAutenticacion { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    [CascadingParameter]
    private HttpContext ContextoHttp { get; set; } = default!;

    private DatosCambio _datos = new();

    /// <summary>
    /// Datos del formulario. Blazor asigna <c>null</c> cuando el POST
    /// pertenece a otro formulario de la página, así que el <c>set</c> nunca
    /// deja la propiedad nula. No lleva inicializador de propiedad para no
    /// disparar la advertencia BL0008.
    /// </summary>
    [SupplyParameterFromForm]
    private DatosCambio Datos
    {
        get => _datos;
        set => _datos = value ?? new DatosCambio();
    }

    private string? Error { get; set; }

    private bool Obligatorio { get; set; }

    protected override void OnInitialized()
    {
        Obligatorio = ContextoHttp.User.DebeCambiarPassword();
    }

    private async Task GuardarAsync()
    {
        Error = null;

        if (!string.Equals(Datos.Nueva, Datos.Repetida, StringComparison.Ordinal))
        {
            Error = "Las dos contraseñas nuevas no coinciden.";
            return;
        }

        var usuarioId = ContextoHttp.User.ObtenerUsuarioId();

        if (usuarioId is null)
        {
            Navegacion.NavigateTo(RutasCuenta.Login, forceLoad: true);
            return;
        }

        var resultado = await ServicioAutenticacion.CambiarPasswordAsync(
            usuarioId.Value,
            Datos.Actual,
            Datos.Nueva);

        if (resultado.EsFallo)
        {
            Error = resultado.Error ?? "No se ha podido cambiar la contraseña.";
            return;
        }

        // Se renueva la cookie para que el claim DebeCambiarPassword quede a false.
        var identidad = await ServicioAutenticacion.ObtenerIdentidadAsync(usuarioId.Value);

        if (identidad is not null)
        {
            await SesionCookie.EntrarAsync(ContextoHttp, identidad, recordarme: true);
        }

        Navegacion.NavigateTo("/", forceLoad: true);
    }

    /// <summary>Datos del formulario de cambio de contraseña.</summary>
    private sealed class DatosCambio
    {
        [Required(ErrorMessage = "Escribe tu contraseña actual.")]
        public string Actual { get; set; } = string.Empty;

        [Required(ErrorMessage = "Escribe la contraseña nueva.")]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string Nueva { get; set; } = string.Empty;

        [Required(ErrorMessage = "Repite la contraseña nueva.")]
        public string Repetida { get; set; } = string.Empty;
    }
}
