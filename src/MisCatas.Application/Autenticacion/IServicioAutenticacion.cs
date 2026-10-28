using MisCatas.Application.Comun;
using MisCatas.Domain.Enums;

namespace MisCatas.Application.Autenticacion;

/// <summary>Identidad resultante de un inicio de sesión correcto.</summary>
/// <param name="UsuarioId">Identificador del usuario.</param>
/// <param name="Login">Login del usuario.</param>
/// <param name="NombreCompleto">Nombre completo para mostrar.</param>
/// <param name="Rol">Rol que determina permisos y página de inicio.</param>
/// <param name="DebeCambiarPassword">Si hay que forzar el cambio de contraseña.</param>
public record IdentidadUsuario(
    int UsuarioId,
    string Login,
    string NombreCompleto,
    Rol Rol,
    bool DebeCambiarPassword);

/// <summary>
/// Valida credenciales y gestiona el cambio de contraseña.
/// No emite la cookie: eso lo hace la capa web.
/// </summary>
public interface IServicioAutenticacion
{
    /// <summary>
    /// Comprueba login y contraseña. Devuelve un fallo genérico si no
    /// coinciden o si el usuario está desactivado, para no revelar
    /// qué logins existen.
    /// </summary>
    Task<Resultado<IdentidadUsuario>> ValidarCredencialesAsync(
        string login,
        string contrasena,
        CancellationToken ct = default);

    /// <summary>
    /// Cambia la contraseña del usuario comprobando primero la actual
    /// y desactiva el aviso de cambio obligatorio.
    /// </summary>
    Task<Resultado> CambiarPasswordAsync(
        int usuarioId,
        string contrasenaActual,
        string contrasenaNueva,
        CancellationToken ct = default);

    /// <summary>Obtiene la identidad de un usuario por su identificador.</summary>
    Task<IdentidadUsuario?> ObtenerIdentidadAsync(
        int usuarioId,
        CancellationToken ct = default);
}
