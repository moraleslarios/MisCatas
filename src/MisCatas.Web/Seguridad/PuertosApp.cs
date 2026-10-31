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
    /// </summary>
    public const int Http = 5080;

    /// <summary>Puerto cifrado, el que de verdad atiende la aplicación.</summary>
    public const int Https = 5443;
}
