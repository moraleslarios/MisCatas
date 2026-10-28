using Microsoft.AspNetCore.Components;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Botella de vino dibujada en SVG con la etiqueta real compuesta encima.
/// Es la pieza visual central de los resultados de una cata.
/// </summary>
public partial class BotellaVirtual
{
    /// <summary>
    /// Identificador único de esta instancia. Los <c>clipPath</c> y los
    /// degradados de un SVG viven en un espacio de nombres global del
    /// documento, así que si hay varias botellas en la misma página hay que
    /// diferenciar sus identificadores o todas usarían los de la primera.
    /// </summary>
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Silueta de la botella bordelesa: cuello estrecho, hombro marcado y
    /// cuerpo recto. Dibujada sobre un lienzo de 120 x 360.
    /// </summary>
    private const string SiluetaBotella =
        "M 46 8 " +
        "C 46 5, 48 4, 50 4 L 70 4 C 72 4, 74 5, 74 8 " +
        "L 74 82 " +
        "C 74 96, 78 104, 86 114 " +
        "C 94 124, 98 136, 98 150 " +
        "L 98 342 " +
        "C 98 351, 93 356, 84 356 " +
        "L 36 356 " +
        "C 27 356, 22 351, 22 342 " +
        "L 22 150 " +
        "C 22 136, 26 124, 34 114 " +
        "C 42 104, 46 96, 46 82 " +
        "Z";

    private const string ColorVidrio = "#2A3A2C";
    private const string ColorVidrioLuz = "#4A6450";
    private const string ColorVidrioBorde = "#101A13";

    /// <summary>
    /// URL de la imagen de la etiqueta. Normalmente
    /// <c>/imagenes/vinos/{id}/etiqueta</c>. Si está vacía se dibuja un hueco
    /// con el número de orden, que es lo que se ve en una cata ciega.
    /// </summary>
    [Parameter]
    public string? UrlEtiqueta { get; set; }

    /// <summary>
    /// Número de orden del vino. Se muestra dentro de la etiqueta cuando no
    /// hay imagen, para respetar la regla de cata ciega.
    /// </summary>
    [Parameter]
    public int? NumeroOrden { get; set; }

    /// <summary>Texto bajo la botella. Opcional.</summary>
    [Parameter]
    public string? Pie { get; set; }

    /// <summary>
    /// Descripción para lectores de pantalla. Si no se indica, se genera a
    /// partir del número de orden.
    /// </summary>
    [Parameter]
    public string? Etiqueta { get; set; }

    /// <summary>Altura de la botella.</summary>
    [Parameter]
    public TamanoBotella Tamano { get; set; } = TamanoBotella.Media;

    /// <summary>Clases CSS extra para el contenedor.</summary>
    [Parameter]
    public string? Clase { get; set; }

    /// <summary>
    /// Puesto en la clasificación. Si se indica, se cuelga un medallón del
    /// cuello de la botella con el número, en oro, plata, bronce o neutro.
    /// </summary>
    [Parameter]
    public int? Puesto { get; set; }

    /// <summary>
    /// Nota media del vino. Si se indica, se estampa en un sello sobre el
    /// hombro de la botella: de un vistazo se compara una vitrina entera.
    /// </summary>
    [Parameter]
    public string? Nota { get; set; }

    private bool TieneEtiqueta => !string.IsNullOrWhiteSpace(UrlEtiqueta);

    private bool TieneMedallon => Puesto is > 0;

    private bool TieneSello => !string.IsNullOrWhiteSpace(Nota) && Nota != "—";

    /// <summary>Colores del medallón según el puesto.</summary>
    private (string Fondo, string Borde, string Texto) ColoresMedallon => Puesto switch
    {
        1 => ("#C9A227", "#8A6D10", "#2B2100"),
        2 => ("#C7C9CE", "#8E9096", "#2B2D31"),
        3 => ("#C98B5A", "#8C5A2F", "#2E1A0C"),
        _ => ("#F7F3EE", "#B9AC9A", "#4A4240")
    };

    private string TextoAccesible
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Etiqueta))
            {
                return Etiqueta!;
            }

            if (NumeroOrden is int numero)
            {
                return TieneEtiqueta
                    ? $"Botella del vino número {numero} con su etiqueta"
                    : $"Botella del vino número {numero}, etiqueta oculta hasta el final de la cata";
            }

            return TieneEtiqueta ? "Botella con su etiqueta" : "Botella sin etiqueta";
        }
    }

    private string ClasesFinales
    {
        get
        {
            var tamano = Tamano switch
            {
                TamanoBotella.Diminuta => "botella--diminuta",
                TamanoBotella.Pequena => "botella--pequena",
                TamanoBotella.Media => "botella--media",
                TamanoBotella.Grande => "botella--grande",
                TamanoBotella.Escaparate => "botella--escaparate",
                TamanoBotella.Elastica => "botella--elastica",
                _ => "botella--media"
            };

            return string.IsNullOrWhiteSpace(Clase)
                ? $"botella {tamano}"
                : $"botella {tamano} {Clase.Trim()}";
        }
    }
}

/// <summary>Alturas predefinidas de <see cref="BotellaVirtual"/>.</summary>
public enum TamanoBotella
{
    /// <summary>Miniatura para listas y rankings.</summary>
    Pequena = 0,

    /// <summary>Tamaño habitual en móvil.</summary>
    Media = 1,

    /// <summary>Protagonista de una pantalla de resultado.</summary>
    Grande = 2,

    /// <summary>Altura relativa a la pantalla, para la TV del escaparate.</summary>
    Escaparate = 3,

    /// <summary>Sello para una fila de tabla o una pastilla de lista.</summary>
    Diminuta = 4,

    /// <summary>
    /// Sin altura propia: la hereda del hueco que le deje el contenedor.
    /// Es la que se usa en las vitrinas, donde el tamaño sale de la rejilla.
    /// </summary>
    Elastica = 5
}
