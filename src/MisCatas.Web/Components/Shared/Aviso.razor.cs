using Microsoft.AspNetCore.Components;

namespace MisCatas.Web.Components.Shared;

/// <summary>Aviso corto de error o de confirmación.</summary>
public partial class Aviso
{
    /// <summary>Texto a mostrar. Si está vacío, no se pinta nada.</summary>
    [Parameter]
    public string? Texto { get; set; }

    /// <summary>Indica si el aviso es de error (rojo) o de éxito (verde).</summary>
    [Parameter]
    public bool EsError { get; set; }
}
