using Microsoft.AspNetCore.Components;

namespace MisCatas.Web.Components.Shared;

/// <summary>Navegación entre páginas de un listado.</summary>
public partial class Paginador
{
    [Parameter]
    public int Pagina { get; set; } = 1;

    [Parameter]
    public int TotalPaginas { get; set; }

    [Parameter]
    public int TotalElementos { get; set; }

    /// <summary>Se invoca con el número de página solicitado.</summary>
    [Parameter]
    public EventCallback<int> AlCambiarPagina { get; set; }

    private bool TieneAnterior => Pagina > 1;

    private bool TieneSiguiente => Pagina < TotalPaginas;

    private Task AnteriorAsync() =>
        TieneAnterior ? AlCambiarPagina.InvokeAsync(Pagina - 1) : Task.CompletedTask;

    private Task SiguienteAsync() =>
        TieneSiguiente ? AlCambiarPagina.InvokeAsync(Pagina + 1) : Task.CompletedTask;
}
