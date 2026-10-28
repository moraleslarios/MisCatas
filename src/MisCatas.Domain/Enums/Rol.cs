namespace MisCatas.Domain.Enums;

/// <summary>
/// Rol de un usuario dentro de la aplicación.
/// </summary>
public enum Rol
{
    /// <summary>Participante normal: vota en las catas a las que se le invita.</summary>
    Usuario = 0,

    /// <summary>Administrador: gestiona usuarios, vinos, catas y configuración.</summary>
    Admin = 1,

    /// <summary>
    /// Usuario especial pensado para una pantalla/TV compartida.
    /// Solo visualiza; nunca se le fuerza el cambio de contraseña.
    /// </summary>
    Escaparate = 2
}
