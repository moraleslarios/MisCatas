using MisCatas.Application.Comun;

namespace MisCatas.Application.Vinos;

/// <summary>Gestión del catálogo de vinos y de sus imágenes.</summary>
public interface IServicioVinos
{
    /// <summary>Obtiene una página de vinos, opcionalmente filtrada por texto.</summary>
    /// <param name="busqueda">Texto a buscar en nombre, bodega, clase o dueño.</param>
    /// <param name="pagina">Número de página (empieza en 1).</param>
    /// <param name="tamanoPagina">Elementos por página.</param>
    /// <param name="ct">Token de cancelación.</param>
    Task<ResultadoPaginado<VinoDto>> ListarAsync(
        string? busqueda = null,
        int pagina = 1,
        int tamanoPagina = 20,
        CancellationToken ct = default);

    /// <summary>Obtiene todos los vinos ordenados por nombre, para selectores.</summary>
    Task<IReadOnlyList<VinoDto>> ListarTodosAsync(CancellationToken ct = default);

    /// <summary>Obtiene un vino por su identificador.</summary>
    Task<VinoDto?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Crea un vino y devuelve su identificador.</summary>
    Task<Resultado<int>> CrearAsync(GuardarVinoDto datos, CancellationToken ct = default);

    /// <summary>Modifica un vino existente.</summary>
    Task<Resultado> EditarAsync(GuardarVinoDto datos, CancellationToken ct = default);

    /// <summary>
    /// Elimina un vino. Falla si está asignado a alguna cata o tiene votos.
    /// </summary>
    Task<Resultado> EliminarAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Sustituye una de las imágenes del vino. El flujo se redimensiona a
    /// 1200 px como máximo y se recomprime a JPEG antes de guardarlo.
    /// </summary>
    Task<Resultado> GuardarImagenAsync(
        int vinoId,
        TipoImagenVino tipo,
        Stream contenido,
        string nombreArchivo,
        CancellationToken ct = default);

    /// <summary>Borra una de las imágenes del vino.</summary>
    Task<Resultado> BorrarImagenAsync(
        int vinoId,
        TipoImagenVino tipo,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene una imagen para servirla por un endpoint HTTP.
    /// Nunca se embeben las imágenes en el HTML.
    /// </summary>
    Task<ImagenVinoDto?> ObtenerImagenAsync(
        int vinoId,
        TipoImagenVino tipo,
        CancellationToken ct = default);
}
