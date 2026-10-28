using MisCatas.Domain.Enums;

namespace MisCatas.Domain.Entidades;

/// <summary>
/// Persona que usa la aplicación. No hay registro público: los crea un administrador.
/// </summary>
public class Usuario
{
    /// <summary>Clave primaria.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Identificador con el que se inicia sesión. Obligatorio y único en todo el sistema.
    /// Se sugiere automáticamente al dar de alta (nombre + primer apellido, normalizado),
    /// pero el administrador puede editarlo.
    /// </summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Nombre de pila. Obligatorio.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Apellidos. Obligatorio.</summary>
    public string Apellidos { get; set; } = string.Empty;

    /// <summary>
    /// Teléfono. Obligatorio porque es la contraseña inicial del usuario
    /// (se almacena hasheada, nunca en claro).
    /// </summary>
    public string Telefono { get; set; } = string.Empty;

    /// <summary>
    /// Hash de la contraseña generado con <c>PasswordHasher</c> de ASP.NET Core Identity.
    /// Nunca se guarda la contraseña en texto plano.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Rol que determina los permisos y la página de inicio.</summary>
    public Rol Rol { get; set; } = Rol.Usuario;

    /// <summary>
    /// Si es <c>true</c> se obliga a cambiar la contraseña en el primer acceso.
    /// Se activa al crear el usuario y al resetear la contraseña al teléfono.
    /// Nunca se aplica al rol <see cref="Rol.Escaparate"/>.
    /// </summary>
    public bool DebeCambiarPassword { get; set; } = true;

    /// <summary>Si es <c>false</c> el usuario no puede iniciar sesión.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Nombre completo para mostrar en pantalla.</summary>
    public string NombreCompleto => $"{Nombre} {Apellidos}".Trim();

    // --- Propiedades de navegación ---

    /// <summary>Catas a las que este usuario está invitado.</summary>
    public ICollection<CataUsuario> Participaciones { get; set; } = new List<CataUsuario>();

    /// <summary>Votos emitidos por este usuario.</summary>
    public ICollection<Voto> Votos { get; set; } = new List<Voto>();
}
