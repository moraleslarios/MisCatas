using Microsoft.AspNetCore.Components;
using MisCatas.Application.Comun;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Pinta un código QR a partir de un texto, normalmente una dirección web.
///
/// El dibujo se hace en el servidor y se mete en la página como SVG en línea,
/// que es la única forma limpia de hacerlo aquí: la aplicación no usa
/// JavaScript en ningún sitio y no hay por qué empezar por esto.
/// </summary>
public partial class CodigoQr
{
    /// <summary>Lo que se codifica. Si está vacío, no se pinta nada.</summary>
    [Parameter]
    public string? Texto { get; set; }

    /// <summary>
    /// Descripción para quien no puede ver la imagen (lectores de pantalla).
    /// </summary>
    [Parameter]
    public string TextoAlternativo { get; set; } = "Código QR";

    /// <summary>Rótulo opcional debajo del código.</summary>
    [Parameter]
    public string? Pie { get; set; }

    /// <summary>Tamaño del cuadro.</summary>
    [Parameter]
    public TamanoQr Tamano { get; set; } = TamanoQr.Normal;

    /// <summary>Clases extra para encajarlo en la pantalla que lo use.</summary>
    [Parameter]
    public string? Clase { get; set; }

    [Inject]
    private IServicioCodigoQr Generador { get; set; } = default!;

    /// <summary>SVG ya montado, o <c>null</c> si no hay nada que pintar.</summary>
    private MarkupString? Dibujo { get; set; }

    private string? _textoPintado;

    private string ClasesFinales
    {
        get
        {
            var clases = $"qr {ClaseTamano}";

            return string.IsNullOrWhiteSpace(Clase) ? clases : $"{clases} {Clase}";
        }
    }

    private string ClaseTamano => Tamano switch
    {
        TamanoQr.Pequeno => "qr--pequeno",
        TamanoQr.Grande => "qr--grande",
        TamanoQr.Escaparate => "qr--escaparate",
        _ => "qr--normal"
    };

    protected override void OnParametersSet()
    {
        // El dibujo solo se recalcula cuando cambia el texto: en el escaparate
        // esta pantalla se repinta varias veces por segundo y no tiene sentido
        // montar el SVG cada vez.
        if (_textoPintado == Texto)
        {
            return;
        }

        _textoPintado = Texto;

        var svg = Generador.Svg(Texto);

        Dibujo = svg is null ? null : new MarkupString(svg);
    }
}

/// <summary>Tamaños previstos para el código QR.</summary>
public enum TamanoQr
{
    /// <summary>Pequeño, para meterlo en una fila o una tarjeta.</summary>
    Pequeno = 0,

    /// <summary>Tamaño corriente de pantalla.</summary>
    Normal = 1,

    /// <summary>Grande, para enseñarlo desde el otro lado de la mesa.</summary>
    Grande = 2,

    /// <summary>Enorme, para que se lea desde el sofá en la televisión.</summary>
    Escaparate = 3,
}
