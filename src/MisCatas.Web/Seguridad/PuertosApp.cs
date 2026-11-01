namespace MisCatas.Web.Seguridad;

/// <summary>
/// Puertos por los que escucha la aplicación.
///
/// Están aquí en lugar de en <c>appsettings.json</c> porque el arranque los
/// necesita en dos sitios a la vez (al configurar Kestrel y al construir la
/// redirección de HTTP a HTTPS), y tenerlos duplicados en la configuración
/// era una fuente garantizada de descuadres.
/// </summary>
public static class PuertosApp
{
    /// <summary>
    /// Puerto sin cifrar. Solo sirve para redirigir a HTTPS: así los enlaces
    /// y los QR antiguos siguen llevando a algún sitio.
    ///
    /// Es el 5081 y no el 5080 por una razón muy concreta: el 5080 es el
    /// puerto <b>seguro</b> por el que IIS atiende a los móviles y al
    /// televisor (ver <c>tools\desplegar-iis.ps1</c>). Si la aplicación
    /// arrancada a mano también abriese el 5080 sin cifrar, las dos cosas se
    /// pelearían por el mismo número y lo que llegase por ahí dependería de
    /// quién hubiera arrancado primero: desde el móvil se veía una conexión
    /// cortada en lugar de la web.
    /// </summary>
    public const int Http = 5081;

    /// <summary>Puerto cifrado, el que de verdad atiende la aplicación.</summary>
    public const int Https = 5443;
}
