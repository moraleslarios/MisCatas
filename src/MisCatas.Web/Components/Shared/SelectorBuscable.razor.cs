using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Hoja superpuesta para elegir una fila de una lista larga escribiendo.
///
/// Sustituye a los desplegables nativos allí donde el catálogo crece: con
/// docenas de vinos o de catadores, un <c>select</c> obliga a recorrer a ciegas
/// una lista interminable. Aquí se teclea cualquier trozo del nombre y la lista
/// se recorta al instante.
///
/// No habla con el servidor: recibe las opciones ya cargadas por la página y
/// devuelve el identificador elegido. Por eso responde sin esperas.
/// </summary>
public partial class SelectorBuscable
{
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    private ElementReference _campo;
    private bool _visibleAnterior;

    /// <summary>Si el buscador está en pantalla. Admite <c>@bind-Visible</c>.</summary>
    [Parameter]
    public bool Visible { get; set; }

    /// <summary>Notifica el cambio de <see cref="Visible"/>.</summary>
    [Parameter]
    public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>Filas elegibles, ya filtradas por la página (sin las repetidas).</summary>
    [Parameter]
    public IReadOnlyList<OpcionSelector> Opciones { get; set; } = [];

    /// <summary>Rótulo pequeño sobre el título.</summary>
    [Parameter]
    public string Antetitulo { get; set; } = "Elegir";

    /// <summary>Título de la hoja.</summary>
    [Parameter]
    public string Titulo { get; set; } = "Buscar";

    /// <summary>Texto guía del campo de búsqueda.</summary>
    [Parameter]
    public string Marcador { get; set; } = "Escribe para buscar…";

    /// <summary>Qué se dice cuando no queda ninguna opción que ofrecer.</summary>
    [Parameter]
    public string TextoSinOpciones { get; set; } = "No queda nada por elegir.";

    /// <summary>Palabra con la que se cuentan las filas en singular.</summary>
    [Parameter]
    public string NombreSingular { get; set; } = "resultado";

    /// <summary>Palabra con la que se cuentan las filas en plural.</summary>
    [Parameter]
    public string NombrePlural { get; set; } = "resultados";

    /// <summary>Se ejecuta con el identificador de la fila elegida.</summary>
    [Parameter]
    public EventCallback<int> AlElegir { get; set; }

    /// <summary>Se ejecuta al cerrar sin elegir nada.</summary>
    [Parameter]
    public EventCallback AlCerrar { get; set; }

    private string Busqueda { get; set; } = string.Empty;

    /// <summary>Filas que encajan con lo escrito, en el orden en que llegaron.</summary>
    private IReadOnlyList<OpcionSelector> Filtradas
    {
        get
        {
            var aguja = OpcionSelector.TextoNormalizado(Busqueda);

            return aguja.Length == 0
                ? Opciones
                : [.. Opciones.Where(o => o.Encaja(aguja))];
        }
    }

    /// <summary>Pie con la cuenta de filas, en castellano y bien concordado.</summary>
    private string TextoCuenta
    {
        get
        {
            var cuantas = Filtradas.Count;

            return cuantas == 1
                ? $"1 {NombreSingular}"
                : $"{cuantas} {NombrePlural}";
        }
    }

    /// <summary>Medallón de la fila: lo que diga la opción o sus iniciales.</summary>
    private static string InicialDe(OpcionSelector opcion)
    {
        if (!string.IsNullOrWhiteSpace(opcion.Inicial))
        {
            return opcion.Inicial!;
        }

        var trozos = opcion.Titulo.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (trozos.Length == 0)
        {
            return "·";
        }

        return trozos.Length == 1
            ? trozos[0][..1].ToUpperInvariant()
            : string.Concat(trozos[0][..1], trozos[1][..1]).ToUpperInvariant();
    }

    protected override async Task OnAfterRenderAsync(bool primeraVez)
    {
        // Al abrirse, el foco va al buscador: así se puede empezar a escribir
        // sin tocar nada más, que es justo el motivo de este componente.
        if (Visible && !_visibleAnterior)
        {
            _visibleAnterior = true;
            Busqueda = string.Empty;
            await _campo.FocusAsync();
        }
        else if (!Visible && _visibleAnterior)
        {
            _visibleAnterior = false;
        }
    }

    private void AlEscribir(ChangeEventArgs argumentos) =>
        Busqueda = argumentos.Value?.ToString() ?? string.Empty;

    private async Task LimpiarAsync()
    {
        Busqueda = string.Empty;
        await _campo.FocusAsync();
    }

    private async Task AlPulsarTeclaAsync(KeyboardEventArgs argumentos)
    {
        if (argumentos.Key is "Escape" or "Esc")
        {
            await CerrarAsync();
        }
    }

    private async Task ElegirAsync(OpcionSelector opcion)
    {
        await OcultarAsync();

        if (AlElegir.HasDelegate)
        {
            await AlElegir.InvokeAsync(opcion.Id);
        }
    }

    private async Task CerrarAsync()
    {
        await OcultarAsync();

        if (AlCerrar.HasDelegate)
        {
            await AlCerrar.InvokeAsync();
        }
    }

    private async Task OcultarAsync()
    {
        Busqueda = string.Empty;

        if (VisibleChanged.HasDelegate)
        {
            await VisibleChanged.InvokeAsync(false);
        }
        else
        {
            Visible = false;
        }
    }
}
