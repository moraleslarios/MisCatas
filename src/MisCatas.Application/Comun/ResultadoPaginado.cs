namespace MisCatas.Application.Comun;

/// <summary>Una página de resultados junto al total de elementos disponibles.</summary>
/// <typeparam name="T">Tipo de los elementos.</typeparam>
/// <param name="Elementos">Elementos de la página actual.</param>
/// <param name="TotalElementos">Número total de elementos sin paginar.</param>
/// <param name="Pagina">Número de página actual (empieza en 1).</param>
/// <param name="TamanoPagina">Número de elementos por página.</param>
public record ResultadoPaginado<T>(
    IReadOnlyList<T> Elementos,
    int TotalElementos,
    int Pagina = 1,
    int TamanoPagina = 20)
{
    /// <summary>Número total de páginas.</summary>
    public int TotalPaginas => TamanoPagina <= 0
        ? 0
        : (int)Math.Ceiling(TotalElementos / (double)TamanoPagina);

    /// <summary>Indica si existe una página anterior.</summary>
    public bool TieneAnterior => Pagina > 1;

    /// <summary>Indica si existe una página siguiente.</summary>
    public bool TieneSiguiente => Pagina < TotalPaginas;

    /// <summary>Página vacía, útil como valor por defecto.</summary>
    public static ResultadoPaginado<T> Vacia(int tamanoPagina = 20) =>
        new(Array.Empty<T>(), 0, 1, tamanoPagina);
}
