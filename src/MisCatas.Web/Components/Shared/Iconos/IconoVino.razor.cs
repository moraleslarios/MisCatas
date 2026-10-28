using Microsoft.AspNetCore.Components;

namespace MisCatas.Web.Components.Shared.Iconos;

/// <summary>
/// Dibuja uno de los iconos del catálogo de <see cref="Icono"/> como SVG inline.
/// No depende de fuentes de iconos ni de recursos externos.
/// </summary>
public partial class IconoVino
{
    /// <summary>Icono que se quiere dibujar.</summary>
    [Parameter]
    [EditorRequired]
    public Icono Nombre { get; set; }

    /// <summary>
    /// Texto alternativo. Si se deja vacío, el icono se considera decorativo
    /// y se oculta a los lectores de pantalla (lo normal cuando va acompañado
    /// de un texto visible que ya transmite el significado).
    /// </summary>
    [Parameter]
    public string? Etiqueta { get; set; }

    /// <summary>
    /// Tamaño del icono. Se traduce a utilidades de ancho y alto de Tailwind.
    /// </summary>
    [Parameter]
    public TamanoIcono Tamano { get; set; } = TamanoIcono.Medio;

    /// <summary>Grosor del trazo. 1.6 es el valor de la identidad visual.</summary>
    [Parameter]
    public double Grosor { get; set; } = 1.6;

    /// <summary>Clases CSS extra que se añaden al elemento <c>svg</c>.</summary>
    [Parameter]
    public string? Clase { get; set; }

    private bool EsDecorativo => string.IsNullOrWhiteSpace(Etiqueta);

    private string ClasesFinales
    {
        get
        {
            var tamano = Tamano switch
            {
                TamanoIcono.Diminuto => "w-3.5 h-3.5",
                TamanoIcono.Pequeno => "w-4 h-4",
                TamanoIcono.Medio => "w-5 h-5",
                TamanoIcono.Grande => "w-7 h-7",
                TamanoIcono.Enorme => "w-10 h-10",
                TamanoIcono.Gigante => "w-16 h-16",
                TamanoIcono.Heredado => "w-[1em] h-[1em]",
                _ => "w-5 h-5"
            };

            return string.IsNullOrWhiteSpace(Clase)
                ? $"icono-vino {tamano}"
                : $"icono-vino {tamano} {Clase.Trim()}";
        }
    }
}

/// <summary>Tamaños predefinidos para <see cref="IconoVino"/>.</summary>
public enum TamanoIcono
{
    /// <summary>14 px. Para pastillas muy pequeñas.</summary>
    Diminuto = 0,

    /// <summary>16 px. Junto a texto pequeño.</summary>
    Pequeno = 1,

    /// <summary>20 px. Tamaño por defecto, el de los botones.</summary>
    Medio = 2,

    /// <summary>28 px. Encabezados de tarjeta.</summary>
    Grande = 3,

    /// <summary>40 px. Estados vacíos en móvil.</summary>
    Enorme = 4,

    /// <summary>64 px. Escaparate y pantallas de bienvenida.</summary>
    Gigante = 5,

    /// <summary>Se adapta al tamaño de fuente del contenedor (1em).</summary>
    Heredado = 6
}
