using Microsoft.AspNetCore.Components;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Una pieza de la vitrina: una botella con su rótulo, su nota y, si procede,
/// un enlace a su detalle.
/// </summary>
/// <param name="Titulo">Rótulo principal: nombre del vino o «Vino nº 3».</param>
/// <param name="Subtitulo">Bodega, D.O. o quién lo trajo.</param>
/// <param name="Nota">Nota media ya formateada, o null si no hay.</param>
/// <param name="Detalle">Línea fina de apoyo: votos, precio, tiempos…</param>
/// <param name="NumeroOrden">Número de orden del vino en la cata.</param>
/// <param name="UrlEtiqueta">
/// URL de la etiqueta real. Debe ser null mientras la cata siga a ciegas.
/// </param>
/// <param name="Puesto">Puesto en la clasificación, para el medallón.</param>
/// <param name="Enlace">Destino al pulsar la pieza. Opcional.</param>
/// <param name="Destacada">
/// Si es cierto la pieza ocupa el doble de ancho: se reserva para el podio.
/// </param>
public sealed record PiezaVitrina(
    string Titulo,
    string? Subtitulo = null,
    string? Nota = null,
    string? Detalle = null,
    int? NumeroOrden = null,
    string? UrlEtiqueta = null,
    int? Puesto = null,
    string? Enlace = null,
    bool Destacada = false)
{
    /// <summary>Descripción para lectores de pantalla.</summary>
    public string TextoAccesible => string.IsNullOrWhiteSpace(Subtitulo)
        ? $"Botella de {Titulo}"
        : $"Botella de {Titulo}, {Subtitulo}";
}

/// <summary>
/// Rejilla fluida de botellas virtuales. Ver los comentarios del .razor para
/// el criterio de tamaños.
/// </summary>
public partial class VitrinaBotellas
{
    /// <summary>Piezas a mostrar. Si está vacía el componente no pinta nada.</summary>
    [Parameter]
    public IReadOnlyList<PiezaVitrina> Piezas { get; set; } = [];

    /// <summary>Rótulo de la sección. También sirve de etiqueta accesible.</summary>
    [Parameter]
    public string Titulo { get; set; } = "Vitrina";

    /// <summary>Si el rótulo se pinta o solo se usa para accesibilidad.</summary>
    [Parameter]
    public bool MostrarTitulo { get; set; } = true;

    /// <summary>
    /// Si la nota se estampa en el sello de la propia botella (true) o se
    /// escribe debajo, junto al rótulo (false).
    /// </summary>
    [Parameter]
    public bool MostrarSello { get; set; } = true;

    /// <summary>Densidad de la rejilla.</summary>
    [Parameter]
    public DensidadVitrina Densidad { get; set; } = DensidadVitrina.Media;

    /// <summary>Clases CSS extra para el contenedor.</summary>
    [Parameter]
    public string? Clase { get; set; }

    /// <summary>
    /// Qué hacer al pulsar una pieza. Si se indica, la pieza se pinta como
    /// botón y <see cref="PiezaVitrina.Enlace"/> se ignora: así la vitrina
    /// puede abrir una ficha sin navegar ni sacar al catador de la pantalla.
    /// </summary>
    [Parameter]
    public EventCallback<PiezaVitrina> AlPulsarPieza { get; set; }

    private string ClasesFinales
    {
        get
        {
            var densidad = Densidad switch
            {
                DensidadVitrina.Compacta => "vitrina--compacta",
                DensidadVitrina.Media => "vitrina--media",
                DensidadVitrina.Amplia => "vitrina--amplia",
                DensidadVitrina.Escaparate => "vitrina--escaparate",
                _ => "vitrina--media"
            };

            return string.IsNullOrWhiteSpace(Clase)
                ? $"vitrina {densidad}"
                : $"vitrina {densidad} {Clase.Trim()}";
        }
    }

    private static string ClasePieza(PiezaVitrina pieza)
    {
        var clases = "vitrina-pieza";

        if (pieza.Destacada)
        {
            clases += " vitrina-pieza--destacada";
        }

        if (pieza.Puesto is int puesto && puesto <= 3)
        {
            clases += $" vitrina-pieza--puesto{puesto}";
        }

        return clases;
    }
}

/// <summary>Cuánto hueco se le da a cada botella de la vitrina.</summary>
public enum DensidadVitrina
{
    /// <summary>Muchas botellas pequeñas: listados largos.</summary>
    Compacta = 0,

    /// <summary>Equilibrio por defecto para el móvil.</summary>
    Media = 1,

    /// <summary>Pocas botellas grandes: pantallas de resultado.</summary>
    Amplia = 2,

    /// <summary>Tamaños pensados para verse desde el fondo de la sala.</summary>
    Escaparate = 3
}
