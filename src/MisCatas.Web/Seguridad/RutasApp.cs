namespace MisCatas.Web.Seguridad;

/// <summary>
/// Rutas de la aplicación en un solo sitio, para no repetir cadenas sueltas
/// por los componentes.
/// </summary>
public static class RutasApp
{
    // --- Usuario ---

    /// <summary>Listado de catas del usuario que ha entrado.</summary>
    public const string MisCatas = "/";

    /// <summary>Pantalla de votación de una cata.</summary>
    public static string Votar(int cataId) => $"/catas/{cataId}/votar";

    /// <summary>Resumen en vivo de una cata (sin revelar notas).</summary>
    public static string Resumen(int cataId) => $"/catas/{cataId}/resumen";

    /// <summary>Resultado completo de una cata finalizada.</summary>
    public static string Resultado(int cataId) => $"/catas/{cataId}/resultado";

    // --- Escaparate ---

    /// <summary>Pantalla única del modo escaparate.</summary>
    public const string Escaparate = "/escaparate";

    // --- Admin ---

    /// <summary>Panel principal del administrador.</summary>
    public const string AdminInicio = "/admin";

    /// <summary>Listado de usuarios.</summary>
    public const string AdminUsuarios = "/admin/usuarios";

    /// <summary>Listado de vinos.</summary>
    public const string AdminVinos = "/admin/vinos";

    /// <summary>Listado de catas.</summary>
    public const string AdminCatas = "/admin/catas";

    /// <summary>Ajustes generales.</summary>
    public const string AdminAjustes = "/admin/ajustes";

    /// <summary>Panel de control de una cata en directo.</summary>
    public static string AdminControlar(int cataId) => $"/admin/catas/{cataId}/control";

    /// <summary>Pantalla de inicio que corresponde a cada rol.</summary>
    public static string InicioSegunRol(Domain.Enums.Rol rol) => rol switch
    {
        Domain.Enums.Rol.Admin => AdminInicio,
        Domain.Enums.Rol.Escaparate => Escaparate,
        _ => MisCatas,
    };
}
