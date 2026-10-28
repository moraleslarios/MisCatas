using MisCatas.Application.Comun;

namespace MisCatas.Application.Usuarios;

/// <summary>
/// Gestión de usuarios. Toda la lógica de negocio vive aquí, nunca en los
/// componentes Blazor.
/// </summary>
public interface IServicioUsuarios
{
    /// <summary>Obtiene una página de usuarios, opcionalmente filtrada por texto.</summary>
    /// <param name="busqueda">Texto a buscar en login, nombre, apellidos o teléfono.</param>
    /// <param name="pagina">Número de página (empieza en 1).</param>
    /// <param name="tamanoPagina">Elementos por página.</param>
    /// <param name="incluirInactivos">Si se incluyen los usuarios desactivados.</param>
    /// <param name="ct">Token de cancelación.</param>
    Task<ResultadoPaginado<UsuarioDto>> ListarAsync(
        string? busqueda = null,
        int pagina = 1,
        int tamanoPagina = 20,
        bool incluirInactivos = true,
        CancellationToken ct = default);

    /// <summary>Obtiene todos los usuarios activos ordenados por nombre, para selectores.</summary>
    Task<IReadOnlyList<UsuarioDto>> ListarActivosAsync(CancellationToken ct = default);

    /// <summary>Obtiene un usuario por su identificador.</summary>
    Task<UsuarioDto?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Sugiere un login a partir del nombre y el primer apellido, normalizando
    /// acentos y espacios, y añadiendo un sufijo numérico si ya existe.
    /// </summary>
    Task<string> SugerirLoginAsync(
        string nombre,
        string apellidos,
        CancellationToken ct = default);

    /// <summary>
    /// Crea un usuario. La contraseña inicial es su teléfono y se marca
    /// <c>DebeCambiarPassword</c> salvo para el rol Escaparate.
    /// </summary>
    Task<Resultado<int>> CrearAsync(CrearUsuarioDto datos, CancellationToken ct = default);

    /// <summary>Modifica los datos de un usuario. No cambia su contraseña.</summary>
    Task<Resultado> EditarAsync(EditarUsuarioDto datos, CancellationToken ct = default);

    /// <summary>
    /// Restablece la contraseña al teléfono del usuario y fuerza el cambio
    /// en el siguiente inicio de sesión.
    /// </summary>
    Task<Resultado> ResetearPasswordAsync(int id, CancellationToken ct = default);

    /// <summary>Activa o desactiva un usuario.</summary>
    Task<Resultado> CambiarActivoAsync(int id, bool activo, CancellationToken ct = default);

    /// <summary>
    /// Elimina un usuario. Falla si tiene votos o participaciones registradas;
    /// en ese caso hay que desactivarlo en lugar de borrarlo.
    /// </summary>
    Task<Resultado> EliminarAsync(int id, CancellationToken ct = default);
}
