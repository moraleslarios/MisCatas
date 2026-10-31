namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Una fila elegible dentro de <see cref="SelectorBuscable"/>.
///
/// Es a propósito un tipo tonto y plano: el selector no sabe si por detrás hay
/// un vino, un usuario o cualquier otra cosa, solo pinta tres líneas de texto y
/// devuelve el identificador que le dieron. Así el mismo buscador sirve para
/// todas las listas largas de la aplicación.
/// </summary>
/// <param name="Id">Identificador que se devuelve al elegir la fila.</param>
/// <param name="Titulo">Línea principal, la que se lee de un vistazo.</param>
/// <param name="Subtitulo">Línea de apoyo: quién lo trajo, el login, etc.</param>
/// <param name="Detalle">Dato suelto que se alinea a la derecha (año, teléfono…).</param>
/// <param name="Inicial">Texto corto del medallón redondo. Si va vacío se calcula.</param>
public sealed record OpcionSelector(
    int Id,
    string Titulo,
    string? Subtitulo = null,
    string? Detalle = null,
    string? Inicial = null)
{
    /// <summary>
    /// Todo el texto de la fila junto, que es contra lo que se busca. Se calcula
    /// una sola vez por fila porque el filtrado se repite en cada tecla.
    /// </summary>
    public string TextoBusqueda { get; } =
        TextoNormalizado($"{Titulo} {Subtitulo} {Detalle}");

    /// <summary>Si la fila encaja con lo que se ha escrito en el buscador.</summary>
    public bool Encaja(string busquedaNormalizada) =>
        busquedaNormalizada.Length == 0
        || TextoBusqueda.Contains(busquedaNormalizada, StringComparison.Ordinal);

    /// <summary>
    /// Pasa un texto a minúsculas y sin tildes, para que «Alión» aparezca
    /// escribiendo «alion» y nadie tenga que pelearse con el acento.
    /// </summary>
    public static string TextoNormalizado(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var descompuesto = texto.Normalize(System.Text.NormalizationForm.FormD);
        var limpio = new System.Text.StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            var categoria = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(caracter);

            if (categoria != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                limpio.Append(char.ToLowerInvariant(caracter));
            }
        }

        return limpio.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}
