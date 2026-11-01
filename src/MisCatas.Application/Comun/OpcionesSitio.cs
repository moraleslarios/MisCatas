namespace MisCatas.Application.Comun;

/// <summary>
/// Datos del sitio que la aplicación no puede deducir por sí misma.
///
/// El caso que obliga a esto: cuando se genera un enlace para enviarlo por
/// fuera (WhatsApp, por ejemplo), no sirve la dirección con la que está
/// navegando el organizador. Él entra por <c>localhost</c> o por el nombre de
/// su equipo, y eso no resuelve desde el móvil de un invitado. La dirección
/// buena solo la sabe quien ha montado la red, así que se configura a mano.
/// </summary>
public class OpcionesSitio
{
    /// <summary>Clave de la sección en <c>appsettings.json</c>.</summary>
    public const string Seccion = "Sitio";

    /// <summary>
    /// Dirección por la que los invitados llegan a la aplicación desde sus
    /// móviles. Por ejemplo <c>https://192.168.1.50:5443</c> en una red local,
    /// o <c>https://miscatas.ejemplo.com</c> si está publicada.
    ///
    /// Vacía mientras no se configure: en ese caso los avisos se siguen
    /// pudiendo enviar, pero sin enlace y advirtiendo de ello.
    /// </summary>
    public string? UrlPublica { get; set; }

    /// <summary>
    /// Segunda dirección, la que solo vale DENTRO del ordenador donde corre la
    /// aplicación. Normalmente el nombre bonito (<c>https://miscatas.es</c>).
    ///
    /// Por qué existe además de <see cref="UrlPublica"/>: ese nombre está dado
    /// de alta en el fichero «hosts» de este equipo y en ningún otro sitio, así
    /// que desde un móvil o desde el televisor no lleva a ninguna parte. Pero
    /// en este ordenador es la cómoda de teclear, y antes de haber dos campos
    /// se perdía: al poner la IP (que es la que sí funciona fuera) el nombre
    /// desaparecía de los QR y de los mensajes.
    ///
    /// Nunca se mete en un código QR: los QR se leen siempre con un móvil.
    /// </summary>
    public string? UrlEnEsteEquipo { get; set; }

    /// <summary>
    /// Prefijo telefónico del país, sin el «+». España es el 34.
    ///
    /// Hace falta porque los teléfonos se guardan como los escribe la gente
    /// (nueve cifras, sin prefijo) y WhatsApp exige el número internacional
    /// completo.
    /// </summary>
    public string? PrefijoTelefonico { get; set; } = "34";

    /// <summary>Prefijo telefónico en cifras, o <c>null</c> si no es válido.</summary>
    public string? PrefijoTelefonicoLimpio
    {
        get
        {
            if (string.IsNullOrWhiteSpace(PrefijoTelefonico))
            {
                return null;
            }

            var cifras = new string(PrefijoTelefonico.Where(char.IsDigit).ToArray());

            return cifras.Length is >= 1 and <= 4 ? cifras : null;
        }
    }

    /// <summary>
    /// Dirección pública lista para pegar en un mensaje, o <c>null</c> si no
    /// está configurada o no es una dirección absoluta válida.
    ///
    /// Se exige que sea absoluta porque un enlace relativo, fuera de la
    /// aplicación, no lleva a ningún sitio.
    /// </summary>
    public string? UrlPublicaLimpia
    {
        get
        {
            if (string.IsNullOrWhiteSpace(UrlPublica))
            {
                return null;
            }

            var texto = UrlPublica.Trim().TrimEnd('/');

            if (!Uri.TryCreate(texto, UriKind.Absolute, out var url))
            {
                return null;
            }

            return url.Scheme is "http" or "https" ? texto : null;
        }
    }

    /// <summary>
    /// <see cref="UrlEnEsteEquipo"/> lista para pegar en un mensaje, o
    /// <c>null</c> si no está configurada o no es una dirección válida.
    /// </summary>
    public string? UrlEnEsteEquipoLimpia
    {
        get
        {
            if (string.IsNullOrWhiteSpace(UrlEnEsteEquipo))
            {
                return null;
            }

            var texto = UrlEnEsteEquipo.Trim().TrimEnd('/');

            if (!Uri.TryCreate(texto, UriKind.Absolute, out var url))
            {
                return null;
            }

            return url.Scheme is "http" or "https" ? texto : null;
        }
    }
}
