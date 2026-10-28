namespace MisCatas.Web.Seguridad;

/// <summary>Nombres de los claims propios y de las políticas de autorización.</summary>
public static class ClaimsMisCatas
{
    /// <summary>Nombre y apellidos del usuario, para mostrar en la cabecera.</summary>
    public const string NombreCompleto = "miscatas:nombre_completo";

    /// <summary>Indica si el usuario debe cambiar la contraseña antes de seguir.</summary>
    public const string DebeCambiarPassword = "miscatas:debe_cambiar_password";
}

/// <summary>Nombres de las políticas de autorización registradas.</summary>
public static class Politicas
{
    /// <summary>Solo administradores.</summary>
    public const string SoloAdmin = "SoloAdmin";

    /// <summary>Usuarios que votan (administradores incluidos).</summary>
    public const string Votantes = "Votantes";

    /// <summary>Pantalla de escaparate (y administradores, para poder probarla).</summary>
    public const string Pantalla = "Pantalla";
}

/// <summary>Rutas fijas de la aplicación relacionadas con la cuenta.</summary>
public static class RutasCuenta
{
    public const string Login = "/cuenta/entrar";
    public const string Logout = "/cuenta/salir";
    public const string CambiarPassword = "/cuenta/cambiar-password";
    public const string AccesoDenegado = "/cuenta/sin-permiso";
}
