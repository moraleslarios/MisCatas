using Microsoft.AspNetCore.Components;
using MisCatas.Domain.Enums;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Tarjeta que resume una cata. Recibe los datos sueltos para poder usarse
/// con cualquier DTO (el del usuario y el de admin) sin acoplarse a ninguno.
/// </summary>
public partial class TarjetaCata
{
    /// <summary>Nombre de la cata.</summary>
    [Parameter]
    [EditorRequired]
    public string Nombre { get; set; } = "";

    /// <summary>Texto en versalitas sobre el nombre. Por ejemplo «En curso».</summary>
    [Parameter]
    public string? Antetitulo { get; set; }

    /// <summary>Lugar de celebración. Opcional.</summary>
    [Parameter]
    public string? Lugar { get; set; }

    /// <summary>Fecha de la cata.</summary>
    [Parameter]
    public DateTime Fecha { get; set; } = DateTime.Today;

    /// <summary>Estado de la cata, que decide el color de la pastilla.</summary>
    [Parameter]
    public EstadoCata Estado { get; set; } = EstadoCata.Borrador;

    /// <summary>Número de vinos de la cata.</summary>
    [Parameter]
    public int NumeroVinos { get; set; }

    /// <summary>Número de participantes. Si es <c>null</c> no se muestra.</summary>
    [Parameter]
    public int? NumeroParticipantes { get; set; }

    /// <summary>
    /// Votos que ya ha emitido el usuario actual. Si es <c>null</c> no se
    /// muestra la línea de progreso (caso del panel de admin).
    /// </summary>
    [Parameter]
    public int? MisVotos { get; set; }

    /// <summary>Destino del botón principal. Si está vacío no se dibuja.</summary>
    [Parameter]
    public string? Enlace { get; set; }

    /// <summary>Texto del botón principal.</summary>
    [Parameter]
    public string TextoAccion { get; set; } = "Ver";

    /// <summary>Resalta la tarjeta: es la cata en curso.</summary>
    [Parameter]
    public bool Destacada { get; set; }

    /// <summary>Contenido extra entre los datos y el botón.</summary>
    [Parameter]
    public RenderFragment? Pie { get; set; }

    /// <summary>Acciones alternativas al botón principal.</summary>
    [Parameter]
    public RenderFragment? Acciones { get; set; }

    /// <summary>Clases CSS extra para el contenedor.</summary>
    [Parameter]
    public string? Clase { get; set; }

    private string NombreEstado => Estado switch
    {
        EstadoCata.Borrador => "Borrador",
        EstadoCata.Abierta => "Abierta",
        EstadoCata.EnCurso => "En curso",
        EstadoCata.Finalizada => "Finalizada",
        _ => Estado.ToString()
    };

    private string ClasePastilla => Estado switch
    {
        EstadoCata.Borrador => "mc-pastilla-borrador",
        EstadoCata.Abierta => "mc-pastilla-abierta",
        EstadoCata.EnCurso => "mc-pastilla-encurso",
        EstadoCata.Finalizada => "mc-pastilla-finalizada",
        _ => "mc-pastilla"
    };

    private string ClaseBoton => Destacada
        ? "mc-boton mc-boton-primario mc-boton-grande tarjeta-cata-boton"
        : "mc-boton mc-boton-secundario tarjeta-cata-boton";

    private string ClasesFinales
    {
        get
        {
            var basico = Destacada ? "tarjeta-cata tarjeta-cata--destacada" : "tarjeta-cata";

            return string.IsNullOrWhiteSpace(Clase)
                ? basico
                : $"{basico} {Clase.Trim()}";
        }
    }
}
