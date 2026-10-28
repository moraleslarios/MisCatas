using Microsoft.AspNetCore.Components;
using MisCatas.Web.Components.Shared.Iconos;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Mensaje que ocupa el hueco de una lista vacía. Explica en español qué
/// pasa y, si procede, ofrece la acción para rellenarla.
/// </summary>
public partial class EstadoVacio
{
    /// <summary>Icono grande que encabeza el mensaje.</summary>
    [Parameter]
    public Icono Icono { get; set; } = Iconos.Icono.Copa;

    /// <summary>Frase principal. Obligatoria: nunca se muestra solo el icono.</summary>
    [Parameter]
    [EditorRequired]
    public string Titulo { get; set; } = "Aquí no hay nada todavía";

    /// <summary>Explicación opcional de por qué está vacío o qué hacer.</summary>
    [Parameter]
    public string? Mensaje { get; set; }

    /// <summary>Botones o enlaces de acción. Opcional.</summary>
    [Parameter]
    public RenderFragment? Acciones { get; set; }

    /// <summary>Clases CSS extra para el contenedor.</summary>
    [Parameter]
    public string? Clase { get; set; }
}
