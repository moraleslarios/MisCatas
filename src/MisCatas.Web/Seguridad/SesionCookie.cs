using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MisCatas.Application.Autenticacion;

namespace MisCatas.Web.Seguridad;

/// <summary>
/// Traduce la identidad del dominio a un <see cref="ClaimsPrincipal"/> y
/// gestiona la cookie de sesión.
/// </summary>
public static class SesionCookie
{
    /// <summary>Construye el principal con los claims que necesita la interfaz.</summary>
    public static ClaimsPrincipal CrearPrincipal(IdentidadUsuario identidad)
    {
        ArgumentNullException.ThrowIfNull(identidad);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identidad.UsuarioId.ToString()),
            new(ClaimTypes.Name, identidad.Login),
            new(ClaimTypes.Role, identidad.Rol.ToString()),
            new(ClaimsMisCatas.NombreCompleto, identidad.NombreCompleto),
            new(ClaimsMisCatas.DebeCambiarPassword,
                identidad.DebeCambiarPassword ? "true" : "false")
        };

        var identidadClaims = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        return new ClaimsPrincipal(identidadClaims);
    }

    /// <summary>Escribe la cookie de autenticación.</summary>
    public static Task EntrarAsync(
        HttpContext contexto,
        IdentidadUsuario identidad,
        bool recordarme)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        return contexto.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            CrearPrincipal(identidad),
            new AuthenticationProperties { IsPersistent = recordarme });
    }

    /// <summary>Borra la cookie de autenticación.</summary>
    public static Task SalirAsync(HttpContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        return contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>Identificador del usuario autenticado, o <c>null</c>.</summary>
    public static int? ObtenerUsuarioId(this ClaimsPrincipal? principal)
    {
        var valor = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(valor, out var id) ? id : null;
    }

    /// <summary>Nombre completo del usuario autenticado.</summary>
    public static string ObtenerNombreCompleto(this ClaimsPrincipal? principal) =>
        principal?.FindFirst(ClaimsMisCatas.NombreCompleto)?.Value
        ?? principal?.Identity?.Name
        ?? string.Empty;

    /// <summary>Indica si el usuario tiene pendiente cambiar la contraseña.</summary>
    public static bool DebeCambiarPassword(this ClaimsPrincipal? principal) =>
        string.Equals(
            principal?.FindFirst(ClaimsMisCatas.DebeCambiarPassword)?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Indica si el usuario tiene el rol indicado.</summary>
    public static bool TieneRol(this ClaimsPrincipal? principal, Domain.Enums.Rol rol) =>
        principal?.IsInRole(rol.ToString()) == true;
}
