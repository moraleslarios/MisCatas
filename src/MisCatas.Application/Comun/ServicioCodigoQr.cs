using System.Text;
using QRCoder;

namespace MisCatas.Application.Comun;

/// <summary>
/// Implementación de <see cref="IServicioCodigoQr"/> con QRCoder.
///
/// El SVG se compone a mano a partir de la matriz de cuadraditos en vez de
/// usar el generador de imágenes de la librería: así no hace falta ninguna
/// dependencia de dibujo del sistema, el resultado es texto plano (se puede
/// incrustar directamente en la página) y escala sin perder nitidez porque
/// lleva <c>viewBox</c> y no medidas fijas.
/// </summary>
public class ServicioCodigoQr : IServicioCodigoQr
{
    /// <summary>
    /// Tope de caracteres. Un QR da para más, pero a partir de aquí los
    /// cuadraditos salen tan finos que ningún móvil los lee de lejos.
    /// </summary>
    private const int LargoMaximo = 600;

    /// <inheritdoc />
    public string? Svg(
        string? texto,
        string colorModulos = "#000000",
        string colorFondo = "#FFFFFF")
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > LargoMaximo)
        {
            return null;
        }

        using var generador = new QRCodeGenerator();

        // Corrección de errores media: aguanta un pliegue o un reflejo sin
        // crecer demasiado.
        using var datos = generador.CreateQrCode(
            texto.Trim(),
            QRCodeGenerator.ECCLevel.M);

        var matriz = datos.ModuleMatrix;
        var lado = matriz.Count;

        if (lado == 0)
        {
            return null;
        }

        var trazo = new StringBuilder();

        for (var fila = 0; fila < lado; fila++)
        {
            var bits = matriz[fila];

            var columna = 0;
            while (columna < lado)
            {
                if (!bits[columna])
                {
                    columna++;
                    continue;
                }

                // Se juntan los cuadraditos negros seguidos en un solo trazo
                // horizontal: el SVG queda mucho más corto.
                var inicio = columna;
                while (columna < lado && bits[columna])
                {
                    columna++;
                }

                var ancho = columna - inicio;

                trazo.Append($"M{inicio} {fila}h{ancho}v1h-{ancho}z");
            }
        }

        var svg = new StringBuilder();

        svg.Append(
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {lado} {lado}" shape-rendering="crispEdges" role="img">""");
        svg.Append($"""<rect width="{lado}" height="{lado}" fill="{Limpio(colorFondo)}"/>""");
        svg.Append($"""<path d="{trazo}" fill="{Limpio(colorModulos)}"/>""");
        svg.Append("</svg>");

        return svg.ToString();
    }

    /// <summary>
    /// Deja el color en algo que no pueda romper el SVG. Si llega cualquier
    /// cosa rara, se cae a negro.
    /// </summary>
    private static string Limpio(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return "#000000";
        }

        var texto = color.Trim();

        var valido = texto.All(c =>
            char.IsAsciiLetterOrDigit(c) || c is '#' or '(' or ')' or ',' or '.' or '%' or ' ');

        return valido ? texto : "#000000";
    }
}
