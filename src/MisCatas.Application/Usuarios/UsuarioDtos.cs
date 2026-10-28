using MisCatas.Domain.Enums;

namespace MisCatas.Application.Usuarios;

/// <summary>Datos de un usuario para listados y detalle.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="Login">Identificador de inicio de sesión.</param>
/// <param name="Nombre">Nombre de pila.</param>
/// <param name="Apellidos">Apellidos.</param>
/// <param name="Telefono">Teléfono (también contraseña inicial).</param>
/// <param name="Rol">Rol asignado.</param>
/// <param name="DebeCambiarPassword">Si se le forzará a cambiar la contraseña.</param>
/// <param name="Activo">Si puede iniciar sesión.</param>
public record UsuarioDto(
    int Id,
    string Login,
    string Nombre,
    string Apellidos,
    string Telefono,
    Rol Rol,
    bool DebeCambiarPassword,
    bool Activo)
{
    /// <summary>Nombre completo para mostrar en pantalla.</summary>
    public string NombreCompleto => $"{Nombre} {Apellidos}".Trim();
}

/// <summary>Datos necesarios para crear un usuario.</summary>
public class CrearUsuarioDto
{
    /// <summary>
    /// Identificador de inicio de sesión. Si se deja vacío se genera a partir
    /// del nombre y el primer apellido.
    /// </summary>
    public string? Login { get; set; }

    /// <summary>Nombre de pila. Obligatorio.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Apellidos. Obligatorio.</summary>
    public string Apellidos { get; set; } = string.Empty;

    /// <summary>
    /// Teléfono. Obligatorio: es la contraseña inicial del usuario.
    /// </summary>
    public string Telefono { get; set; } = string.Empty;

    /// <summary>Rol asignado.</summary>
    public Rol Rol { get; set; } = Rol.Usuario;

    /// <summary>Si el usuario puede iniciar sesión.</summary>
    public bool Activo { get; set; } = true;
}

/// <summary>Datos necesarios para modificar un usuario existente.</summary>
public class EditarUsuarioDto
{
    /// <summary>Identificador del usuario a modificar.</summary>
    public int Id { get; set; }

    /// <summary>Identificador de inicio de sesión.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Nombre de pila.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Apellidos.</summary>
    public string Apellidos { get; set; } = string.Empty;

    /// <summary>Teléfono.</summary>
    public string Telefono { get; set; } = string.Empty;

    /// <summary>Rol asignado.</summary>
    public Rol Rol { get; set; } = Rol.Usuario;

    /// <summary>Si el usuario puede iniciar sesión.</summary>
    public bool Activo { get; set; } = true;
}
